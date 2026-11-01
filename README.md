# Bài thực hành 2, 3, 4 — các dịch vụ SOA

Solution .NET 10 gồm ba ứng dụng chạy độc lập:

| Dịch vụ | URL mặc định | Dữ liệu |
| --- | --- | --- |
| `BaiThucHanh2` — xác thực JWT | http://localhost:5194 | Tài khoản mẫu của bài 2 |
| `ProductService` — sản phẩm | http://localhost:5001 | SQLite `ProductService/products.db` |
| `OrderService` — đơn hàng | http://localhost:5002 | SQLite `OrderService/orders.db` |

## Chạy trong Rider hoặc terminal

Cài .NET SDK 10, mở `BaiThucHanh2.sln`, chạy ba project với profile `http`.
Hoặc mở ba terminal tại thư mục chứa solution và chạy lần lượt:

```powershell
dotnet run --project BaiThucHanh2 --launch-profile http
```

```powershell
dotnet run --project ProductService --launch-profile http
```

```powershell
dotnet run --project OrderService --launch-profile http
```

ProductService và OrderService tự áp dụng migration khi khởi động. Đường dẫn SQLite mặc định tính theo thư mục làm việc của service; `dotnet run --project` chạy trong thư mục project. Khi chạy DLL trực tiếp, đặt working directory tương ứng hoặc cấu hình đường dẫn database tuyệt đối.

Mở Swagger đơn hàng: http://localhost:5002/swagger.

## Đăng nhập và thử API

1. Gửi `POST http://localhost:5194/login` với JSON:

   ```json
   {
     "userName": "admin",
     "password": "e10adc3949ba59abbe56e057f20f883e"
   }
   ```

   API bài 2 hiện nhận chuỗi MD5 của mật khẩu `123456`.

2. Sao chép `token` trả về, nhập vào **Authorize** trong Swagger đơn hàng, hoặc gửi header `Authorization: Bearer <token>`.
3. Gửi `GET http://localhost:5001/products` để xem ID và tồn kho hiện có.
4. Gửi `POST http://localhost:5002/orders`:

   ```json
   {
     "customerName": "Nguyễn Văn An",
     "customerEmail": "an@example.com",
     "items": [
       { "productId": 1, "quantity": 1 },
       { "productId": 2, "quantity": 2 }
     ]
   }
   ```

Với dữ liệu seed ban đầu của ProductService, tổng tiền là `40000000`. Dùng ID thực tế trả về để thử các API tiếp theo. File `OrderService/OrderService.http` có sẵn mẫu cho toàn bộ endpoint.

## REST API bài 4

Tất cả endpoint dưới đây yêu cầu token được Auth Service bài 2 chấp nhận:

| Method | Đường dẫn | Chức năng / request body |
| --- | --- | --- |
| GET | `/orders` | Danh sách đơn hàng kèm chi tiết |
| GET | `/orders/{id}` | Một đơn hàng kèm chi tiết |
| POST | `/orders` | Tạo đơn: `customerName`, `customerEmail`, `items` |
| PUT | `/orders/{id}` | Cập nhật trạng thái: `{ "status": "completed" }` |
| DELETE | `/orders/{id}` | Xóa đơn và toàn bộ chi tiết của đơn |
| GET | `/order_items` | Danh sách tất cả chi tiết đơn hàng |
| GET | `/order_items/{id}` | Một chi tiết đơn hàng |
| POST | `/order_items` | `{ "orderId": 1, "productId": 2, "quantity": 1 }` |
| PUT | `/order_items/{id}` | `{ "productId": 2, "quantity": 3 }` |
| DELETE | `/order_items/{id}` | Xóa mặt hàng và tính lại tổng đơn |

`GET /health` và Swagger là public. POST thành công trả `201` và header `Location`; các thao tác khác thành công trả `200`. Dữ liệu nghiệp vụ trả trong `{ "success": true, "data": ... }`. Lỗi dữ liệu đầu vào trả `400`, không có token/token sai trả `401`, không tìm thấy đơn hoặc chi tiết trả `404`, thiếu tồn kho hoặc sửa mặt hàng trong đơn đã kết thúc trả `409`. Lỗi kết nối Auth/Product Service trả `503`; dữ liệu sản phẩm sai định dạng trả `502`. API không lưu đơn mới nếu có một mặt hàng không hợp lệ.

## Quy tắc xử lý

- Database riêng có bảng `orders` và `order_items`; các cột dùng snake_case theo đề. `order_id` có khóa ngoại nội bộ và xóa cascade. `product_id` lưu dấu vết ID, giao tiếp với ProductService qua HTTP, không liên kết database giữa các service.
- Mỗi request đơn hàng được xác thực bằng `GET /auth` của bài 2. Bearer token được chuyển tiếp khi lấy thông tin từ ProductService.
- Khi tạo đơn, sản phẩm trùng ID được gộp và kiểm tra tổng số lượng. Thêm/sửa mặt hàng cũng kiểm tra tổng số lượng của cùng sản phẩm trong đơn đó.
- `product_name` và `unit_price` được lấy từ ProductService; client không quyết định giá. Sửa số lượng cùng sản phẩm giữ giá đã lưu. Đổi sản phẩm lấy tên và giá hiện tại của sản phẩm mới.
- Server tính `total_price = quantity * unit_price` và `total_amount = tổng total_price`. Tiền làm tròn hai chữ số, tối đa `99999999.99` theo `DECIMAL(10,2)`; thời gian lưu UTC.
- Đơn mới có trạng thái `pending`; PUT chấp nhận `pending`, `completed`, `cancelled`. Chỉ thêm/sửa/xóa mặt hàng trong đơn `pending`. Xóa hết mặt hàng để lại đơn với tổng tiền `0`.
- Phạm vi bài này là **kiểm tra tồn kho**. Việc tạo đơn không trừ/giữ chỗ tồn kho; nhiều đơn riêng biệt có thể cùng vượt qua kiểm tra. Nếu mở rộng sang bán hàng thực tế, cần thêm API giữ/trừ tồn kho nguyên tử ở ProductService.

## Cấu hình và kiểm tra

`OrderService/appsettings.json` cấu hình `ConnectionStrings:OrderDb`, `Services:AuthUrl`, `Services:ProductUrl`. Có thể ghi đè bằng biến môi trường `ConnectionStrings__OrderDb`, `Services__AuthUrl`, `Services__ProductUrl`; URL service cần dấu `/` cuối.

```powershell
dotnet build BaiThucHanh2.sln
pwsh -File tests/order-service.integration.ps1
```

Script kiểm thử dùng ba service thật trên các cổng tạm, database SQLite mới và log riêng trong `TestResults`; không ghi vào database đang dùng. Script tự dừng các process do nó tạo. Bao gồm CRUD, token sai/thiếu, kiểm tra dữ liệu và tồn kho, tổng tiền, giá snapshot, rollback khi lỗi, xóa cascade, lưu dữ liệu qua khởi động lại và lỗi service phụ thuộc.
