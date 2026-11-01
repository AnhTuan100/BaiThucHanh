#requires -Version 7.4
param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$runDir = Join-Path $repo ('TestResults/order-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $runDir -Force
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$script:checks = 0

function Assert-That($Condition, [string]$Message) {
    if (-not $Condition) { throw "FAILED: $Message" }
    $script:checks++
}

function Send-Api([string]$Method, [string]$Url, $Body, [int]$Expected = 200, [string]$Token = $script:token) {
    $parameters = @{
        Method = $Method; Uri = $Url; SkipHttpErrorCheck = $true
        TimeoutSec = 10; NoProxy = $true; Headers = @{}
    }
    if ($Token) { $parameters.Headers.Authorization = "Bearer $Token" }
    if ($null -ne $Body) {
        $parameters.Body = ConvertTo-Json -InputObject $Body -Depth 10 -Compress
        $parameters.ContentType = 'application/json; charset=utf-8'
    }
    $response = Invoke-WebRequest @parameters
    Assert-That ($response.StatusCode -eq $Expected) "$Method $Url expected $Expected, got $($response.StatusCode): $($response.Content)"
    if ($Expected -eq 201) { Assert-That (-not [string]::IsNullOrWhiteSpace($response.Headers.Location)) 'Created response includes Location header' }
    return ($response.Content | ConvertFrom-Json)
}

function Start-TestApi([string]$Project, [int]$Port, [hashtable]$Settings) {
    $projectDir = Join-Path $repo $Project
    $dll = Join-Path $projectDir "bin/Debug/net10.0/$Project.dll"
    $childEnv = @{ ASPNETCORE_ENVIRONMENT = 'Development' }
    foreach ($key in $Settings.Keys) { $childEnv[$key] = $Settings[$key] }
    $apiProcess = Start-Process -FilePath dotnet -ArgumentList @("`"$dll`"", '--urls', "http://127.0.0.1:$Port") `
        -WorkingDirectory $projectDir -WindowStyle Hidden -Environment $childEnv -PassThru `
        -RedirectStandardOutput (Join-Path $runDir "$Project-$([guid]::NewGuid().ToString('N')).out.log") `
        -RedirectStandardError (Join-Path $runDir "$Project-$([guid]::NewGuid().ToString('N')).err.log")
    $processes.Add($apiProcess)
    $deadline = [DateTime]::UtcNow.AddSeconds(25)
    do {
        if ($apiProcess.HasExited) { throw "$Project exited early. Logs: $runDir" }
        try {
            $response = Invoke-WebRequest "http://127.0.0.1:$Port/swagger/v1/swagger.json" -NoProxy -TimeoutSec 1
            if ($response.StatusCode -eq 200) { return $apiProcess }
        } catch { Start-Sleep -Milliseconds 200 }
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "$Project did not become ready. Logs: $runDir"
}

function Stop-TestApi([System.Diagnostics.Process]$ApiProcess) {
    if (-not $ApiProcess.HasExited) { $ApiProcess.Kill($true); $ApiProcess.WaitForExit() }
}

try {
    if (-not $SkipBuild) {
        dotnet build (Join-Path $repo 'BaiThucHanh2.sln') --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    }

    # Giữ cả ba cổng đến khi cấp phát xong để không chọn trùng nhau.
    $listeners = 1..3 | ForEach-Object {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $listener
    }
    $ports = @($listeners | ForEach-Object { $_.LocalEndpoint.Port })
    $listeners | ForEach-Object { $_.Stop() }
    $auth = "http://127.0.0.1:$($ports[0])"
    $product = "http://127.0.0.1:$($ports[1])"
    $orders = "http://127.0.0.1:$($ports[2])"
    $authProcess = Start-TestApi 'BaiThucHanh2' $ports[0] @{}
    $productProcess = Start-TestApi 'ProductService' $ports[1] @{
        ConnectionStrings__ProductDb = "Data Source=$(Join-Path $runDir 'products.db')"
    }
    $orderSettings = @{
        ConnectionStrings__OrderDb = "Data Source=$(Join-Path $runDir 'orders.db')"
        Services__AuthUrl = "$auth/"; Services__ProductUrl = "$product/"
    }
    $orderProcess = Start-TestApi 'OrderService' $ports[2] $orderSettings

    $null = Send-Api GET "$orders/health" $null 200 ''
    foreach ($path in @('/orders', '/order_items')) {
        $null = Send-Api GET "$orders$path" $null 401 ''
        $null = Send-Api GET "$orders$path" $null 401 'invalid-token'
        foreach ($verb in @('POST', 'PUT', 'DELETE')) {
            $target = if ($verb -eq 'POST') { "$orders$path" } else { "$orders$path/1" }
            $null = Send-Api $verb $target @{} 401 ''
        }
    }
    $login = Send-Api POST "$auth/login" @{ userName = 'admin'; password = 'e10adc3949ba59abbe56e057f20f883e' } 200 ''
    $script:token = $login.token
    Assert-That (-not [string]::IsNullOrWhiteSpace($script:token)) 'Auth Service issued JWT'
    $null = Send-Api GET "$orders/orders" $null 401 ($script:token + 'broken')

    $a = (Send-Api POST "$product/products" @{ name = 'Integration A'; price = 12.34; quantity = 10 } 201).data.id
    $b = (Send-Api POST "$product/products" @{ name = 'Integration B'; price = 5; quantity = 20 } 201).data.id
    $created = Send-Api POST "$orders/orders" @{
        customerName = 'Test Customer'; customerEmail = 'test@example.com'
        items = @(@{ productId = $a; quantity = 2 }, @{ productId = $a; quantity = 1 }, @{ productId = $b; quantity = 2 })
    } 201
    $orderId = $created.data.id
    $aItem = @($created.data.items | Where-Object productId -EQ $a)[0].id
    $bItem = @($created.data.items | Where-Object productId -EQ $b)[0].id
    Assert-That ($created.data.status -eq 'pending' -and $created.data.items.Count -eq 2) 'Default pending and duplicate products merged'
    Assert-That ($created.data.totalAmount -eq 47.02) 'Order total computed with decimal arithmetic'
    $null = Send-Api GET "$orders/orders/$orderId" $null
    $null = Send-Api GET "$orders/order_items/$aItem" $null
    Assert-That (@((Send-Api GET "$orders/order_items" $null).data).Count -eq 2) 'List all items'

    foreach ($payload in @(
        @{ customerName = ''; customerEmail = 'test@example.com'; items = @(@{ productId = $a; quantity = 1 }) },
        @{ customerName = 'Test'; customerEmail = 'invalid'; items = @(@{ productId = $a; quantity = 1 }) },
        @{ customerName = 'Test'; customerEmail = 'test@example.com'; items = @() },
        @{ customerName = 'Test'; customerEmail = 'test@example.com'; items = $null },
        @{ customerName = 'Test'; customerEmail = 'test@example.com'; items = @($null) },
        @{ customerName = 'Test'; customerEmail = 'test@example.com'; items = @(@{ productId = $a; quantity = 0 }) },
        @{ customerName = 'Test'; customerEmail = 'test@example.com'; items = @(@{ productId = -1; quantity = 1 }) },
        @{ customerName = 'Test'; customerEmail = 'test@example.com'; items = @(@{ productId = $a; quantity = 1 }, @{ productId = 2147483647; quantity = 1 }) }
    )) { $null = Send-Api POST "$orders/orders" $payload 400 }
    $null = Send-Api POST "$orders/orders" @{
        customerName = 'Test'; customerEmail = 'test@example.com'
        items = @(@{ productId = $a; quantity = 6 }, @{ productId = $a; quantity = 5 })
    } 409
    Assert-That (@((Send-Api GET "$orders/orders" $null).data).Count -eq 1) 'Failed creates leave no partial orders'
    Assert-That (@((Send-Api GET "$orders/order_items" $null).data).Count -eq 2) 'Failed creates leave no partial items'

    $added = (Send-Api POST "$orders/order_items" @{ orderId = $orderId; productId = $a; quantity = 2 } 201).data.id
    Assert-That ((Send-Api GET "$orders/orders/$orderId" $null).data.totalAmount -eq 71.70) 'Adding item updates order total'
    $null = Send-Api POST "$orders/order_items" @{ orderId = $orderId; productId = $a; quantity = 6 } 409
    $null = Send-Api PUT "$orders/order_items/$aItem" @{ productId = $a; quantity = 9 } 409
    Assert-That ((Send-Api GET "$orders/orders/$orderId" $null).data.totalAmount -eq 71.70) 'Rejected item mutations preserve total'

    $null = Send-Api PUT "$product/products/$a" @{ name = 'Changed A'; price = 20; quantity = 10 }
    $updated = (Send-Api PUT "$orders/order_items/$aItem" @{ productId = $a; quantity = 4 }).data
    Assert-That ($updated.unitPrice -eq 12.34 -and $updated.productName -eq 'Integration A') 'Quantity update preserves original price and name'
    Assert-That ((Send-Api GET "$orders/orders/$orderId" $null).data.totalAmount -eq 84.04) 'Quantity update recalculates total'
    $updated = (Send-Api PUT "$orders/order_items/$aItem" @{ productId = $b; quantity = 3 }).data
    Assert-That ($updated.productId -eq $b -and $updated.unitPrice -eq 5) 'Changing product obtains its snapshot'
    $null = Send-Api DELETE "$orders/order_items/$added" $null
    Assert-That ((Send-Api GET "$orders/orders/$orderId" $null).data.totalAmount -eq 25) 'Deleting item recalculates total'

    $null = Send-Api POST "$orders/order_items" @{ orderId = 2147483647; productId = $a; quantity = 1 } 404
    $null = Send-Api POST "$orders/order_items" @{ orderId = $orderId; productId = $a; quantity = -1 } 400
    $null = Send-Api PUT "$orders/order_items/$aItem" @{ productId = $b; quantity = 0 } 400
    foreach ($path in @('/orders/2147483647', '/order_items/2147483647')) {
        foreach ($verb in @('GET', 'DELETE')) { $null = Send-Api $verb "$orders$path" $null 404 }
    }
    $null = Send-Api PUT "$orders/orders/2147483647" @{ status = 'completed' } 404
    $null = Send-Api PUT "$orders/order_items/2147483647" @{ productId = $a; quantity = 1 } 404
    $null = Send-Api PUT "$orders/orders/$orderId" @{ status = 'unknown' } 400
    $null = Send-Api PUT "$orders/orders/$orderId" @{ status = 'completed' }
    $null = Send-Api POST "$orders/order_items" @{ orderId = $orderId; productId = $a; quantity = 1 } 409
    $null = Send-Api PUT "$orders/order_items/$aItem" @{ productId = $b; quantity = 1 } 409
    $null = Send-Api DELETE "$orders/order_items/$bItem" $null 409
    $null = Send-Api PUT "$orders/orders/$orderId" @{ status = 'cancelled' }
    $null = Send-Api PUT "$orders/orders/$orderId" @{ status = 'pending' }

    $expensive = (Send-Api POST "$product/products" @{ name = 'Expensive'; price = 99999999.99; quantity = 10 } 201).data.id
    $null = Send-Api POST "$orders/orders" @{
        customerName = 'Test'; customerEmail = 'test@example.com'; items = @(@{ productId = $expensive; quantity = 2 })
    } 400
    $emptyOrder = (Send-Api POST "$orders/orders" @{
        customerName = 'Empty Test'; customerEmail = 'empty@example.com'; items = @(@{ productId = $b; quantity = 1 })
    } 201).data
    $null = Send-Api DELETE "$orders/order_items/$($emptyOrder.items[0].id)" $null
    Assert-That ((Send-Api GET "$orders/orders/$($emptyOrder.id)" $null).data.totalAmount -eq 0) 'Deleting last item produces zero total'
    $null = Send-Api DELETE "$orders/orders/$($emptyOrder.id)" $null

    Stop-TestApi $orderProcess
    $orderProcess = Start-TestApi 'OrderService' $ports[2] $orderSettings
    Assert-That ((Send-Api GET "$orders/orders/$orderId" $null).data.totalAmount -eq 25) 'SQLite data persists across service restart'
    $swagger = Invoke-RestMethod "$orders/swagger/v1/swagger.json" -NoProxy
    foreach ($resource in @('orders', 'order_items')) {
        foreach ($verb in @('get', 'post')) {
            Assert-That ($null -ne $swagger.paths."/$resource".$verb) "Swagger exposes $verb /$resource"
        }
        foreach ($verb in @('get', 'put', 'delete')) {
            Assert-That ($null -ne $swagger.paths."/$resource/{id}".$verb) "Swagger exposes $verb /$resource/{id}"
        }
    }

    Stop-TestApi $productProcess
    $null = Send-Api POST "$orders/orders" @{
        customerName = 'Test'; customerEmail = 'test@example.com'; items = @(@{ productId = $a; quantity = 1 })
    } 503
    $null = Send-Api POST "$orders/order_items" @{ orderId = $orderId; productId = $b; quantity = 1 } 503
    $null = Send-Api PUT "$orders/order_items/$aItem" @{ productId = $b; quantity = 1 } 503
    Assert-That ((Send-Api GET "$orders/orders/$orderId" $null).data.totalAmount -eq 25) 'Product outage preserves existing order total'
    Assert-That (@((Send-Api GET "$orders/orders" $null).data).Count -eq 1) 'Product outage does not create an order'
    $null = Send-Api DELETE "$orders/orders/$orderId" $null
    $null = Send-Api GET "$orders/orders/$orderId" $null 404
    $null = Send-Api GET "$orders/order_items/$aItem" $null 404
    Assert-That (@((Send-Api GET "$orders/order_items" $null).data).Count -eq 0) 'Deleting order cascades to items'

    Stop-TestApi $authProcess
    $null = Send-Api GET "$orders/orders" $null 503
    $null = Send-Api GET "$orders/health" $null 200 ''
    Write-Host "PASS: $script:checks assertions. Logs and isolated databases: $runDir"
} finally {
    foreach ($apiProcess in $processes) { Stop-TestApi $apiProcess }
}
