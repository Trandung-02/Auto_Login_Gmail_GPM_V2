Google Drive — upload PDF tự động (tùy chọn "Mở tab Google Drive")
====================================================================

Cấu trúc:
  Data\DriveUpload\
    01\   → 1 file .pdf (upload lên 2 tab Drive)
    02\   → 1 file .pdf (upload lên 2 tab)
    ...
    11\   → 1 file .pdf (upload lên 2 tab)
    12\   → 1 file .pdf (upload lên 3 tab)

Tổng tối đa: 25 tab (11×2 + 1×3) khi ô «số mail cần log» = 0.
Khi ô đó = N (vd. 5): kế hoạch 5 tab — hàng 1 upload thư mục 01, hàng 2→02, … hàng N→0N.

Cách dùng:
  1. Đặt đúng 1 file PDF vào mỗi thư mục 01 … 12 (tên file có thể giống nhau giữa các thư mục).
  2. Nhập số mail cần log (0 = đủ 25 tab; >0 = số tab/hàng tham gia upload).
  3. Bật «Mở tab Google Drive» + «Upload PDF» và chạy đăng nhập.

Lưu ý:
  - Nếu thiếu PDF ở một số thư mục: chỉ mở/upload tab cho thư mục đã có file.
  - Nếu không có PDF nào: app chỉ mở 1 tab My Drive (không upload).
  - Khi chạy, app vẫn tự tạo lại thư mục nếu bị xóa (cạnh PlayAPP.exe\Data\DriveUpload).
