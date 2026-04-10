# Auto Login Gmail GPM V2

Ứng dụng desktop **PlayAPP** (**.NET 8** + **WinForms** + **Microsoft.Playwright**) tự động đăng nhập Gmail, tích hợp **GPM Login API** (cổng **19995**), proxy, và (tùy chọn) pipeline **Google Form + Sheets + Apps Script**.

---

## Mã nguồn & build

- **Project chính:** `PlayAPP_decompiled_proj/PlayAPP.csproj` (mã C# trong `PlayAPP/`, gồm `Form1.cs`, `GpmApi.cs`, `DiagnosticsExport.cs`, …).
- **Giải pháp Visual Studio / `dotnet sln`:** `PlayAPP_decompiled_proj/PlayAPP.sln`.
- **Build Release:**

  ```powershell
  dotnet build PlayAPP_decompiled_proj\PlayAPP.csproj -c Release
  ```

- Output: `PlayAPP_decompiled_proj\bin\Release\net8.0-windows\PlayAPP.dll` (và `PlayAPP.exe` nếu cấu hình xuất exe). Chạy từ thư mục có `Data\`, `.playwright\` (nếu dùng) đúng như bản phân phối.

---

## Kiểm thử (`dotnet test`)

Project **xUnit:** `PlayAPP_decompiled_proj/PlayAPP.Tests/` (kiểm tra URL/helper `GpmApi`).

```powershell
dotnet test PlayAPP_decompiled_proj\PlayAPP.Tests\PlayAPP.Tests.csproj -c Release
```

---

## Git — `.gitignore`

Ở gốc repo có **`.gitignore`**: loại trừ `Data/Account.txt`, `Data/proxy.txt`, log, `Data/screenshots/`, nội dung `datafile/input|used`, thư mục `bin/obj`. Trước khi `git add`, vẫn nên rà soát — không commit dữ liệu thật.

---

## Yêu cầu hệ thống

- **Windows**, **.NET 8 Desktop Runtime**.
- **GPM Login / GPM Browser** mở sẵn để API `http://127.0.0.1:19995` phản hồi.
- Thư mục **`.playwright`** (nếu kèm bản phân phối) cho CLI Playwright khi cần.

---

## Giao diện chính (thanh trên)

| Điều khiển | Ý nghĩa |
|------------|---------|
| **Bắt đầu / Dừng** | Chạy hoặc dừng batch (hủy token chờ, đóng browser). |
| **Thư mục Data** | Mở Explorer tới `Data\`. |
| **ZIP chẩn đoán** | Xuất file ZIP: log (`automation.log`, `login_success.log`, …), `Setting.txt`, tối đa **15** ảnh PNG mới nhất trong `Data/screenshots/`. **Không** gồm `Account.txt` / `proxy.txt`. |
| Trạng thái sidebar | Luồng chạy, OK/Lỗi, tiến độ batch, ước lượng thời gian còn lại (khi đủ dữ liệu). |

---

## `Data\Setting.txt`

Định dạng `key=value` (mỗi dòng). Ứng dụng **merge** key cũ khi lưu; một số khóa chính:

| Khóa | Mô tả |
|------|--------|
| `so_account_log` | Giới hạn số account mỗi lần chạy (**0** = tất cả trong phạm vi đã chọn). |
| `luong` | Số profile chạy song song mỗi lô: **2**, **5** hoặc **10** (tối đa 10 browser/lô). |
| `sudungproxy` | `True` / `False` — dùng `Data\proxy.txt`. |
| `gpm_proxy_group_id` | Id nhóm GPM khi bật proxy (khớp combo trong UI). |
| `changeinfo` | Đổi ảnh / thông tin Gmail (pipeline chỉnh theme). |
| `taoform` | Tạo Form + Sheet + Script (cần URL trong `Account.txt` và nội dung `Data\`). |
| `offchrome` | Đóng Chrome sau mỗi account (và các trường hợp bắt buộc đóng khi tài khoản “chết”). |
| `wait_slice_ms` | Lát chờ nội bộ khi dùng Playwright (50–2000, mặc định **250**) — giúp nút **Dừng** có tác dụng giữa các bước `WaitForTimeout` dài. |
| `script_run_pause_ms` | Thời gian chờ sau mỗi lần bấm **Run** trong editor Apps Script (1000–120000, mặc định **10000**). |
| `form_x`, `form_y`, `form_w`, `form_h`, `form_maximized` | Vị trí / kích thước cửa sổ. |

Lưu UTF-8 nếu có ký tự đặc biệt.

---

## `Data\Account.txt`

Mỗi dòng một tài khoản, phân tách **`|`**:

1. Email Gmail  
2. Mật khẩu  
3. Secret TOTP / App Password (tùy luồng)  
4. Khôi phục / dự phòng  
5. Email phụ (tùy luồng)  
6. URL Google Form (edit)  
7. URL Apps Script  
8. URL Google Sheets  

---

## Luồng nghiệp vụ (tóm tắt)

- **GPM:** Tải profile theo nhóm (khi proxy), gán proxy lên profile, mở Chrome qua CDP, Playwright điều khiển.
- **Login:** `accounts.google.com`, xử lý thách thức, reCAPTCHA/restrictions được log và screenshot theo quy tắc hiện tại.
- **Trùng nhóm:** `Data\login_success.log` — bỏ qua UID đã thành công cùng “nhóm GPM log”.
- **Apps Script:** Sau **Run**, nếu Execution log báo *requires access to your Google Account…* → **F5 (reload)** editor và **Run lại** (có giới hạn số lần); sau OAuth vẫn có thể F5 + Run thêm.
- **Log:** `Data\automation.log` (hàng đợi ghi bất đồng bộ, rotate kích thước).

Chi tiết OAuth, Form/Theme, picker “Select Header” nằm trong mã `Form1.cs` (pipeline dài).

---

## `Fill2faLive`

Tool CLI hỗ trợ điền 2FA qua CDP (xem `Fill2faLive/Program.cs`, `--help`). PlayAPP có thể gọi khi cần.

---

## Cấu trúc thư mục khác

| Đường dẫn | Ghi chú |
|-----------|---------|
| `Data\proxy.txt` | Mỗi dòng một proxy (`host:port:user:pass` …). |
| `Data\codesc.txt`, `tieude.txt`, `noidung.txt` | Nội dung cho script / mail / form. |
| `Checkpoint\`, `datafile\` | Luồng cookie / file input–used (tùy cấu hình). |
| `playwright.ps1` | Gọi CLI Playwright khi cần. |

---

## Bảo mật & điều khoản

- **Account**, **proxy**, **cookie**, **log** là dữ liệu nhạy cảm — không chia sẻ công khai; kiểm tra ZIP trước khi gửi hỗ trợ.
- Chỉ tự động hóa với **tài khoản bạn sở hữu hoặc được ủy quyền**. Vi phạm điều khoản nhà cung cấp (Google, …) là rủi ro pháp lý / khóa tài khoản — bạn tự chịu trách nhiệm.

---

## Lộ trình kỹ thuật (còn việc)

Đã có phần: chờ Playwright theo lát + token batch (`PlaywrightWaitHelpers`, `PageWaitCancellableAsync`), khóa `wait_slice_ms` / `script_run_pause_ms`, và file `Form1.Gpm.cs` (khối GPM). Các hướng tiếp:

1. **Playwright + hủy sâu hơn:** Bọc thêm `WaitFor*` / `GotoAsync` bằng `Task.WhenAny` + token nơi Playwright .NET không hỗ trợ hủy trực tiếp.
2. **Tách `Form1` tiếp:** Gmail, Form/Script, logging thành partial hoặc service riêng.
3. **Timeout / retry:** Mở rộng thêm hằng số chờ (network, reload) trong `Setting.txt` hoặc JSON nếu cần tinh chỉnh từng bước.

---

## Phụ thuộc (tham khảo)

Theo bản build thường gặp: **Microsoft.Playwright**, **Newtonsoft.Json** (phiên bản xem `PlayAPP.deps.json` nếu có trong thư mục chạy).
