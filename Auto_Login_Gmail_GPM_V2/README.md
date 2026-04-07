# Auto Login Gmail GPM V2 — Hướng dẫn sử dụng

Ứng dụng desktop **PlayAPP** (`.NET 8` + **Microsoft.Playwright**) tự động hóa trình duyệt. Trong thư mục này **không có mã nguồn**, chỉ bản build: `PlayAPP.exe`, `PlayAPP.dll` và file cấu hình trong `Data\`.

---

## Yêu cầu hệ thống

- **Windows** (ứng dụng dùng `Microsoft.WindowsDesktop.App`).
- **.NET Runtime 8.0** (Desktop) — khớp với `PlayAPP.runtimeconfig.json`.
- Thư mục **`.playwright`** (đi kèm project): Node/CLI Playwright dùng khi cần cài hoặc quản lý browser cho Playwright.
- Chạy **`PlayAPP.exe`** từ thư mục gốc project để đường dẫn tới `Data\`, `Checkpoint\`, `datafile\` và DLL đồng bộ đúng.

---

## Cấu trúc thư mục (quan trọng)

| Đường dẫn | Mô tả |
|-----------|--------|
| `PlayAPP.exe` | Chương trình chính |
| `Data\` | File cấu hình và nội dung (xem chi tiết bên dưới) |
| `Checkpoint\CheckPoint.txt` | Danh sách tài khoản dạng `UID\|mật_khẩu\|chuỗi_cookie` (Facebook) |
| `datafile\input\` | File cookie đưa vào xử lý (ví dụ tên `Cookie_*.txt`) |
| `datafile\used\` | File đã xử lý xong (ứng dụng có thể chuyển/ghi lại tại đây) |
| `playwright.ps1` | Script PowerShell nạp `Microsoft.Playwright.dll` và gọi CLI Playwright (hỗ trợ lệnh `playwright` tương đương) |
| `.playwright\` | Gói Playwright (Node) kèm theo |

---

## Cấu hình `Data\Setting.txt`

Định dạng `key=value` (mỗi dòng một tham số), ví dụ:

- **`filedata=`** — thường là đường dẫn hoặc tham chiếu tới nguồn dữ liệu (để trống hoặc giá trị do phần mềm quy định).
- **`username=`** — mã định danh/người dùng hoặc profile (số trong bản mẫu: `11`).
- **`luong=`** — số luồng / số tác vụ song song (ví dụ `1`).
- **`sudungproxy=`** — `True` hoặc `False`: có dùng proxy trong `proxy.txt` hay không.
- **`hide=`** — `True` hoặc `False`: thường tương ứng chế độ ẩn cửa sổ / headless (tùy bản build).

Chỉnh sửa bằng Notepad, lưu file UTF-8 nếu có ký tự đặc biệt.

---

## Cấu hình `Data\Account.txt`

Mỗi dòng **một tài khoản**, các trường **phân tách bằng ký tự `|`**:

1. Email Gmail  
2. Mật khẩu  
3. Mật khẩu ứng dụng / mã ứng dụng (App Password) — nếu dùng  
4. Chuỗi khôi phục / mã dự phòng — nếu có  
5. Email (có thể trùng cột 1 — tùy luồng)  
6. URL **Google Form** (chỉnh sửa form)  
7. URL **Google Apps Script** (dự án script)  
8. URL **Google Sheets** (bảng tính liên kết)

Ví dụ minh họa (dùng dữ liệu giả):

```text
email@example.com|MatKhau|AppPassword|Recovery|email@example.com|https://docs.google.com/forms/.../edit|https://script.google.com/.../edit|https://docs.google.com/spreadsheets/d/.../edit
```

---

## `Data\proxy.txt`

Mỗi dòng một proxy, định dạng thường gặp:

```text
host:port:username:password
```

Bật `sudungproxy=True` trong `Setting.txt` khi cần dùng danh sách này.

---

## `Data\linkmain.txt`

Danh sách URL (một dòng một link). Trong bản mẫu là các placeholder `https://your-link.com1` … — thay bằng link thật theo nhu cầu.

---

## `Data\tieude.txt` và `Data\noidung.txt`

- **`tieude.txt`**: Tiêu đề email / nội dung ngắn (một khối văn bản).  
- **`noidung.txt`**: Nội dung email đầy đủ (có thể nhiều dòng).

Ứng dụng hoặc luồng Google Apps Script có thể dùng các file này làm mẫu gửi mail / điền form — cần khớp với cách bạn cấu hình trên Google Sheet và script.

---

## `Data\codesc.txt` (Google Apps Script)

Đây là mã **Google Apps Script** (hàm `shareSingleFormToList`, v.v.) dùng với **Google Sheets** + **MailApp**:

- Trong script có `CONFIG` (FORM_URL, SHEET_NAME, BATCH_LIMIT, thời gian nghỉ, …).  
- Cần **dán script vào** dự án Apps Script** trùng URL đã ghi trong `Account.txt`.  
- **`FORM_URL`** trong script phải khớp form thật (thay `"123456"` / placeholder bằng ID hoặc URL form đúng).  
- Sheet cần có tên trùng **`SHEET_NAME`** (mặc định mẫu: `Sheet1`).  
- Script đọc dữ liệu từ sheet (cột email, tên trang, tiêu đề, nội dung, …) và gửi mail theo lô; có placeholder `[Name]` trong tiêu đề và nội dung.

**Lưu ý:** Quyền gửi mail, trigger thời gian và hạn mức của Google Apps Script do Google quản lý — cần cấp quyền khi chạy lần đầu.

---

## `Checkpoint\CheckPoint.txt`

Mỗi dòng một bản ghi dạng:

```text
Facebook_UID|MatKhau|chuỗi_cookie_day_du
```

Cookie thường là chuỗi dài gồm nhiều cặp `tên=giá trị` phân tách bằng `; `. Dùng cho luồng đăng nhập / duy trì phiên bằng cookie (tùy chức năng bản build).

---

## Thư mục `datafile\`

- Đặt file cookie (định dạng tương tự `UID|pass|cookie`) vào **`input\`** để xử lý.  
- File đã chạy có thể nằm trong **`used\`** (tránh chạy trùng tùy logic ứng dụng).

---

## Chạy chương trình

1. Cài **.NET 8 Desktop Runtime** nếu chưa có.  
2. Kiểm tra / chỉnh `Data\Setting.txt`, `Account.txt`, và các file liên quan.  
3. Mở **`PlayAPP.exe`** (nên chạy với quyền thường; chỉ “Run as administrator” nếu thực sự cần).  
4. Nếu Playwright báo thiếu browser, có thể dùng CLI qua `playwright.ps1` (tham khảo tài liệu Microsoft Playwright cho .NET: `pwsh .\playwright.ps1 install` hoặc lệnh tương đương mà bản build yêu cầu).

---

## Bảo mật và sao lưu

- `Account.txt`, `Checkpoint\CheckPoint.txt`, `proxy.txt` và file trong `datafile\` chứa **thông tin nhạy cảm**. Không chia sẻ công khai; nên sao lưu riêng và giới hạn quyền truy cập thư mục.  
- Trước khi commit lên Git, thêm các file trên vào `.gitignore` hoặc xóa dữ liệu thật khỏi bản sao.

---

## Phụ thuộc (tham khảo)

Theo `PlayAPP.deps.json`:

- **Microsoft.Playwright** 1.58.0  
- **Newtonsoft.Json** 13.0.4  

---

## Ghi chú

- Tên thư mục **GPM** thường gợi ý tích hợp với công cụ quản lý profile trình duyệt (ví dụ GoLogin Profile Manager). Chi tiết kết nối phụ thuộc phiên bản tool và cài đặt trên máy bạn.  
- Không có source C# trong repo: muốn sửa logic phải có project gốc hoặc liên hệ tác giả bản build.
