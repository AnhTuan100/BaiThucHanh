using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

// Real HTTP checks against three processes and isolated SQLite files.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
var temp = Path.Combine(Path.GetTempPath(), "order-service-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var processes = new List<Process>();
var logs = new System.Collections.Concurrent.ConcurrentQueue<string>();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
var authUrl = $"http://127.0.0.1:{FreePort()}/";
var productUrl = $"http://127.0.0.1:{FreePort()}/";
var orderUrl = $"http://127.0.0.1:{FreePort()}/";
var checks = 0;
string? token = null;

try
{
    var auth = Start("BaiThucHanh2", authUrl);
    var products = Start("ProductService", productUrl,
        "--Services:Auth", authUrl, "--ConnectionStrings:Products", $"Data Source={Path.Combine(temp, "products.db")}");
    var orders = StartOrders();
    await Ready(authUrl);
    await Ready(productUrl);
    await Ready(orderUrl);
    await Send(orderUrl, "orders", HttpMethod.Get, null, 401, false);
    await Send(productUrl, "products", HttpMethod.Get, null, 401, false);
    token = "invalid-token";
    await Send(orderUrl, "orders", HttpMethod.Get, null, 401);
    await Send(productUrl, "products", HttpMethod.Get, null, 401);
    await Send(authUrl, "login", HttpMethod.Post, new { userName = "admin", password = "wrong" }, 401, false);
    var login = await Send(authUrl, "login", HttpMethod.Post,
        new { userName = "admin", password = "e10adc3949ba59abbe56e057f20f883e" }, 200, false);
    token = login.GetProperty("token").GetString();
    await Send(authUrl, "auth", HttpMethod.Get, null, 200);
    var catalogue = await Send(productUrl, "products", HttpMethod.Get, null, 200);
    Check(catalogue.GetArrayLength() == 3, "seed products");
    var p = await Send(productUrl, "products", HttpMethod.Post,
        new { name = "Decimal price", price = 12.34m, quantity = 100 }, 201);
    var pid = p.GetProperty("id").GetInt32();
    await Send(productUrl, $"products/{pid}", HttpMethod.Get, null, 200);
    await Send(productUrl, "products", HttpMethod.Post, new { name = "Bad", price = 1.234m, quantity = 1 }, 400);
    await Send(productUrl, "products", HttpMethod.Post, new { name = "Bad", price = 1m, quantity = -1 }, 400);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(3, 1)]), 409);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(999999, 1)]), 404);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(1, 0)]), 400);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([]), 400);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([null]), 400);
    await Send(orderUrl, "orders", HttpMethod.Post,
        new { customer_name = "A", customer_email = "bad", items = new[] { Item(1, 1) } }, 400);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(1, 11), Item(1, 10)]), 409);
    var empty = await Send(orderUrl, "orders", HttpMethod.Get, null, 200);
    Check(empty.GetArrayLength() == 0, "failed requests do not create orders");

    var created = await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(pid, 2), Item(pid, 1)]), 201);
    var oid = created.GetProperty("id").GetInt32();
    var iid = created.GetProperty("items")[0].GetProperty("id").GetInt32();
    Check(created.GetProperty("total_amount").GetDecimal() == 37.02m, "exact decimal total");
    Check(created.GetProperty("items").GetArrayLength() == 1, "duplicate products grouped");
    Check(created.GetProperty("status").GetString() == "pending", "initial status");
    await Send(orderUrl, $"orders/{oid}", HttpMethod.Get, null, 200);
    await Send(orderUrl, "order_items", HttpMethod.Get, null, 200);
    await Send(orderUrl, $"order_items/{iid}", HttpMethod.Get, null, 200);
    await Send(orderUrl, "orders/999999", HttpMethod.Get, null, 404);
    await Send(orderUrl, "order_items/999999", HttpMethod.Get, null, 404);
    await Send(orderUrl, $"orders/{oid}", HttpMethod.Put, new { status = "shipped" }, 400);
    await Send(orderUrl, "order_items", HttpMethod.Post, new { order_id = oid, product_id = pid, quantity = 1 }, 409);
    await Send(orderUrl, "order_items", HttpMethod.Post, new { order_id = 999999, product_id = 1, quantity = 1 }, 404);
    var added = await Send(orderUrl, "order_items", HttpMethod.Post,
        new { order_id = oid, product_id = 2, quantity = 2 }, 201);
    var secondId = added.GetProperty("id").GetInt32();
    var updated = await Send(orderUrl, $"orders/{oid}", HttpMethod.Get, null, 200);
    Check(updated.GetProperty("total_amount").GetDecimal() == 300037.02m, "add recalculates total");
    await Send(productUrl, $"products/{pid}", HttpMethod.Put,
        new { name = "New name", price = 99.99m, quantity = 100 }, 200);
    var snapshot = await Send(orderUrl, $"order_items/{iid}", HttpMethod.Put, Item(pid, 4), 200);
    Check(snapshot.GetProperty("unit_price").GetDecimal() == 12.34m, "historical price preserved");
    Check(snapshot.GetProperty("product_name").GetString() == "Decimal price", "historical name preserved");
    await Send(orderUrl, $"order_items/{iid}", HttpMethod.Put, Item(pid, 101), 409);
    await Send(orderUrl, $"order_items/{iid}", HttpMethod.Put, Item(2, 1), 409);
    await Send(orderUrl, $"order_items/{secondId}", HttpMethod.Put, Item(1, 1), 200);
    updated = await Send(orderUrl, $"orders/{oid}", HttpMethod.Get, null, 200);
    Check(updated.GetProperty("total_amount").GetDecimal() == 250049.36m, "update recalculates total");
    await Send(orderUrl, $"order_items/{secondId}", HttpMethod.Delete, null, 204);
    await Send(orderUrl, $"order_items/{iid}", HttpMethod.Delete, null, 409);
    updated = await Send(orderUrl, $"orders/{oid}", HttpMethod.Get, null, 200);
    Check(updated.GetProperty("total_amount").GetDecimal() == 49.36m, "delete recalculates total");

    // Prove database persistence by restarting the actual order process.
    Stop(orders);
    orders = StartOrders();
    await Ready(orderUrl);
    updated = await Send(orderUrl, $"orders/{oid}", HttpMethod.Get, null, 200);
    Check(updated.GetProperty("total_amount").GetDecimal() == 49.36m, "persisted total after restart");
    await Send(orderUrl, $"orders/{oid}", HttpMethod.Put, new { status = "completed" }, 200);
    await Send(orderUrl, $"orders/{oid}", HttpMethod.Put, new { status = "completed" }, 200);
    await Send(orderUrl, $"orders/{oid}", HttpMethod.Put, new { status = "pending" }, 409);
    await Send(orderUrl, $"order_items/{iid}", HttpMethod.Put, Item(pid, 1), 409);
    await Send(orderUrl, "order_items", HttpMethod.Post, new { order_id = oid, product_id = 1, quantity = 1 }, 409);
    await Send(orderUrl, $"order_items/{iid}", HttpMethod.Delete, null, 409);
    await Send(orderUrl, $"orders/{oid}", HttpMethod.Delete, null, 204);
    await Send(orderUrl, $"order_items/{iid}", HttpMethod.Get, null, 404);
    await Send(orderUrl, $"orders/{oid}", HttpMethod.Delete, null, 404);
    var cancelled = await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(1, 1)]), 201);
    var cancelledId = cancelled.GetProperty("id").GetInt32();
    await Send(orderUrl, $"orders/{cancelledId}", HttpMethod.Put, new { status = "cancelled" }, 200);
    await Send(orderUrl, $"orders/{cancelledId}", HttpMethod.Put, new { status = "completed" }, 409);
    await Send(orderUrl, $"orders/{cancelledId}", HttpMethod.Delete, null, 204);
    await Send(productUrl, $"products/{pid}", HttpMethod.Put,
        new { name = "Expensive", price = 99999999.99m, quantity = 100 }, 200);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(pid, 2)]), 400);
    await Send(productUrl, $"products/{pid}", HttpMethod.Delete, null, 204);
    await Send(productUrl, $"products/{pid}", HttpMethod.Get, null, 404);
    Stop(products);
    await Send(orderUrl, "orders", HttpMethod.Post, Order([Item(1, 1)]), 503);
    empty = await Send(orderUrl, "orders", HttpMethod.Get, null, 200);
    Check(empty.GetArrayLength() == 0, "product outage does not create partial order");
    Stop(auth);
    await Send(orderUrl, "orders", HttpMethod.Get, null, 503);
    Console.WriteLine($"PASS: {checks} integration checks (real auth, product and order services).");
}
catch
{
    foreach (var line in logs.TakeLast(30)) Console.Error.WriteLine(line);
    throw;
}
finally
{
    foreach (var process in processes) { Stop(process); process.Dispose(); }
    // Remove only the unique directory created by this test run.
    Directory.Delete(temp, recursive: true);
}

Process StartOrders() => Start("OrderService", orderUrl,
    "--Services:Auth", authUrl, "--Services:Products", productUrl,
    "--ConnectionStrings:Orders", $"Data Source={Path.Combine(temp, "orders.db")};Foreign Keys=True");

Process Start(string project, string url, params string[] args)
{
    var info = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = Path.Combine(root, project), UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    info.ArgumentList.Add(Path.Combine(root, project, "bin", configuration, "net10.0", project + ".dll"));
    info.ArgumentList.Add("--urls");
    info.ArgumentList.Add(url);
    foreach (var arg in args) info.ArgumentList.Add(arg);
    info.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    var process = Process.Start(info)!;
    processes.Add(process);
    process.OutputDataReceived += (_, e) => { if (e.Data is not null) logs.Enqueue(project + ": " + e.Data); };
    process.ErrorDataReceived += (_, e) => { if (e.Data is not null) logs.Enqueue(project + ": " + e.Data); };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    return process;
}

static void Stop(Process process)
{
    if (process.HasExited) return;
    process.Kill(entireProcessTree: true);
    process.WaitForExit();
}

async Task Ready(string url)
{
    for (var i = 0; i < 100; i++)
    {
        try
        {
            using var response = await http.GetAsync(url + "swagger/v1/swagger.json");
            if (response.IsSuccessStatusCode) return;
        }
        catch (HttpRequestException) { }
        await Task.Delay(100);
    }
    throw new Exception("Service did not start: " + url);
}

async Task<JsonElement> Send(string url, string path, HttpMethod method, object? body, int expected, bool authenticated = true)
{
    using var request = new HttpRequestMessage(method, url + path);
    if (authenticated && token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    if (body is not null) request.Content = JsonContent.Create(body);
    using var response = await http.SendAsync(request);
    var content = await response.Content.ReadAsStringAsync();
    Check((int)response.StatusCode == expected, $"{method} {path}: expected {expected}, got {(int)response.StatusCode}: {content}");
    if (expected == 201) Check(response.Headers.Location is not null, "201 has Location header");
    return string.IsNullOrWhiteSpace(content) ? default : JsonDocument.Parse(content).RootElement.Clone();
}

void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    checks++;
}

static object Order(object?[] items) => new { customer_name = "Nguyen Van A", customer_email = "a@example.com", items };
static object Item(int id, int quantity) => new { product_id = id, quantity };
static int FreePort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}
