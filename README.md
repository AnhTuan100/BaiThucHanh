# Bài thực hành 4 & 5 — Quản lý đơn hàng & Báo cáo

Phát triển từ bài 2 bằng ASP.NET Core .NET 10. Các service chạy thành tiến trình riêng, giao tiếp bằng HTTP, không truy vấn chung cơ sở dữ liệu.

| Service | Cổng mặc định | Dữ liệu |
| --- | --- | --- |
| `BaiThucHanh2` — xác thực có sẵn | 5194 | Tài khoản mẫu của bài 2 |
| `ProductService` — sản phẩm hỗ trợ chạy bài | 5001 | SQLite `products.db` riêng |
| `OrderService` — đơn hàng | 5002 | SQLite `orders.db` riêng |
| `ReportService` — báo cáo (bài 5) | 5003 | SQLite `reports.db` riêng |

`ServiceDefaults` là thư viện cấu hình API, Swagger, xác thực từ xa và hợp đồng HTTP; không chứa DbContext dùng chung. Service sản phẩm tối thiểu được bổ sung vì repository bài 2 chưa có service này.

## Chạy ứng dụng

Cần .NET SDK 10 và kết nối NuGet ở lần restore đầu tiên. Tại thư mục repository:

```powershell
dotnet build BaiThucHanh2.sln
```

Mở ba terminal, mỗi terminal chạy một lệnh:

```powershell
dotnet run --project BaiThucHanh2 --launch-profile http
dotnet run --project ProductService --launch-profile http
dotnet run --project OrderService --launch-profile http
dotnet run --project ReportService --launch-profile http
```

Database và bảng tự tạo lần đầu bằng EF Core `EnsureCreated()`. File SQLite nằm trong thư mục làm việc của từng service, được Git bỏ qua. Với database đã có, `EnsureCreated()` không cập nhật cấu trúc bảng; khi thay đổi schema cần dùng migration hoặc database thử nghiệm mới.

Swagger trong môi trường Development:

- Xác thực: [localhost:5194/swagger](http://localhost:5194/swagger)
- Sản phẩm: [localhost:5001/swagger](http://localhost:5001/swagger)
- Đơn hàng: [localhost:5002/swagger](http://localhost:5002/swagger)
- Báo cáo: [localhost:5003/swagger](http://localhost:5003/swagger)

## Đăng nhập và thử đơn hàng

Gọi `POST http://localhost:5194/login` với tài khoản mẫu bài 2. Trường password của bài 2 nhận chuỗi MD5 đã có sẵn:

```json
{
  "userName": "admin",
  "password": "e10adc3949ba59abbe56e057f20f883e"
}
```

Copy `token` trong phản hồi. Chọn **Authorize** trên Swagger đơn hàng/sản phẩm và dán token. Khi gọi bằng HTTP client, thêm `Authorization: Bearer <token>`.

Mỗi request API đơn hàng/sản phẩm đều gửi token đến `GET /auth` của bài 2. Service đơn hàng gửi tiếp token khi gọi sản phẩm. Không sao chép khóa bí mật JWT sang các service mới. Thiếu/sai token trả `401`; service phụ thuộc không sẵn sàng trả `503`.

Gọi `POST http://localhost:5002/orders`:

```json
{
  "customer_name": "Nguyễn Văn A",
  "customer_email": "a@example.com",
  "items": [
    { "product_id": 1, "quantity": 2 },
    { "product_id": 2, "quantity": 1 }
  ]
}
```

Kết quả `201 Created`, tổng tiền `650000`, trạng thái `pending`. Sản phẩm mẫu: ID 1 bàn phím giá 250000 tồn 20; ID 2 chuột giá 150000 tồn 30; ID 3 màn hình hết hàng.

## REST API

Body và response của hai service mới dùng `snake_case`.

| Method | Đường dẫn | Nội dung |
| --- | --- | --- |
| GET | `/orders` | Danh sách đơn kèm chi tiết |
| GET | `/orders/{id}` | Chi tiết đơn |
| POST | `/orders` | Tạo đơn, lấy giá/tên và kiểm tra tồn kho qua HTTP |
| PUT | `/orders/{id}` | Đổi trạng thái, body `{"status":"completed"}` |
| DELETE | `/orders/{id}` | Xóa đơn và toàn bộ chi tiết |
| GET | `/order_items` | Danh sách chi tiết |
| GET | `/order_items/{id}` | Một chi tiết |
| POST | `/order_items` | Body `{"order_id":1,"product_id":2,"quantity":1}` |
| PUT | `/order_items/{id}` | Body `{"product_id":2,"quantity":3}` |
| DELETE | `/order_items/{id}` | Xóa một chi tiết |

Service sản phẩm hỗ trợ `GET/POST /products`, `GET/PUT/DELETE /products/{id}`. Body tạo/sửa: `{"name":"Chuột","price":150000,"quantity":30}`. Có thể cấu hình `Services:Products` để dùng service sản phẩm khác có cùng hợp đồng HTTP (`id`, `name`, `price`, `quantity`).

## Quy tắc nghiệp vụ

- Bảng `orders` và `order_items` có đầy đủ cột theo đề, khóa chính tự tăng; khóa ngoại chi tiết xóa dây chuyền. Dùng tên `order_items` theo API (phụ lục ghi `other_items` là tên không thống nhất).
- `product_id` là mã tham chiếu từ xa, không có khóa ngoại sang database sản phẩm.
- Chỉ nhận mã sản phẩm và số lượng; tên, đơn giá, thành tiền, tổng tiền và thời gian do server xác định. Thời gian lưu UTC.
- Số lượng phải dương, email hợp lệ, tên tối đa 255 ký tự. Tiền dùng C# `decimal`, tối đa 2 chữ số thập phân, giới hạn 99999999.99 theo `DECIMAL(10,2)`. SQLite không thực thi precision/scale như SQL Server nên API kiểm tra các giới hạn này.
- Đơn mới có ít nhất một mặt hàng. Các dòng trùng sản phẩm khi tạo đơn được gộp trước khi kiểm tra tồn kho. Khi thêm chi tiết trùng trả `409`, cần dùng PUT trên chi tiết hiện có.
- Cập nhật số lượng giữ nguyên tên và giá lúc đặt hàng; đổi sản phẩm lấy tên và giá mới. Thêm/sửa/xóa chi tiết tính lại tổng tiền và cập nhật thời gian trong cùng transaction của database đơn hàng.
- Chỉ sửa chi tiết khi `pending`. Cho phép `pending → completed` hoặc `pending → cancelled`; không mở lại đơn đã kết thúc. Gửi lại cùng trạng thái vẫn thành công.
- Không xóa mặt hàng cuối cùng để giữ đơn hợp lệ; muốn bỏ toàn bộ thì xóa đơn. DELETE đơn xóa cả chi tiết, kể cả đơn đã hoàn tất/hủy theo yêu cầu CRUD của bài.
- Không tìm thấy trả `404`, đầu vào sai trả `400`, thiếu tồn kho hoặc xung đột trạng thái trả `409`.

Phạm vi tồn kho của bài: **kiểm tra số lượng hiện có khi tạo hoặc sửa mặt hàng**, chưa giữ chỗ/trừ/hoàn kho. Vì vậy nhiều đơn có thể cùng vượt tổng tồn kho nếu cộng dồn; để xử lý bán hàng thực tế cần API giữ chỗ tồn kho nguyên tử và cơ chế bù trừ giữa các service. Các tài khoản xác thực dùng chung quyền quản lý đơn theo phạm vi bài thực hành, chưa phân quyền theo chủ sở hữu.

Tham khảo giới hạn kiểu dữ liệu SQLite: [tài liệu Microsoft EF Core](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations).

## REST API — Báo cáo (Bài 5, cổng 5003)

Body và response dùng `snake_case`. Mọi request đều cần `Authorization: Bearer <token>`.

| Method | Đường dẫn | Nội dung |
| --- | --- | --- |
| GET | `/reports/orders` | Danh sách tất cả báo cáo đơn hàng (kèm product_reports) |
| GET | `/reports/orders/{id}` | Chi tiết báo cáo đơn hàng |
| POST | `/reports/orders` | Tạo báo cáo đơn hàng — body `{"order_id":1}` |
| DELETE | `/reports/orders/{id}` | Xóa báo cáo đơn hàng (kéo theo product_reports con) |
| GET | `/reports/products` | Danh sách tất cả báo cáo sản phẩm |
| GET | `/reports/products/{id}` | Chi tiết báo cáo sản phẩm |
| POST | `/reports/products` | Tạo báo cáo tổng hợp theo sản phẩm — body `{"product_id":1,"cost_price":175000}` |
| DELETE | `/reports/products/{id}` | Xóa báo cáo sản phẩm |

### Nghiệp vụ tính toán

- **POST /reports/orders**: Gọi `GET /orders/{order_id}` trên OrderService để lấy chi tiết đơn. Với mỗi `order_item`, tính `revenue = unit_price × quantity`, `cost = unit_price × 0.7 × quantity` (giá vốn ước tính 70%), `profit = revenue - cost`. Tổng hợp vào `orders_reports` và từng dòng vào `product_reports`.
- **POST /reports/products**: Gọi `GET /orders` để lấy toàn bộ đơn hàng, lọc các `order_item` có `product_id` khớp, cộng dồn `total_sold` và `revenue`. `cost = cost_price × total_sold` (người dùng cung cấp giá vốn/đơn vị). `profit = revenue - cost`.
- Mỗi `order_id` / `product_id` chỉ được tạo báo cáo một lần; tạo lại trả `409`.
- Xóa báo cáo đơn hàng sẽ xóa dây chuyền các `product_reports` con.

## Cấu hình

Địa chỉ service và connection string đặt tại `OrderService/appsettings.json`, `ProductService/appsettings.json`, `ReportService/appsettings.json`. Có thể ghi đè bằng biến môi trường `Services__Auth`, `Services__Products`, `Services__Orders`, `ConnectionStrings__Orders`, `ConnectionStrings__Products`, `ConnectionStrings__Reports`. Dùng `--urls` nếu muốn đổi cổng khi chạy; nhớ cập nhật địa chỉ service phụ thuộc tương ứng.
