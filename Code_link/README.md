# Code_link — Landing page (Next.js + Saas UI)

Ứng dụng landing page xây dựng trên [Next.js 14](https://nextjs.org/) (App Router), [Chakra UI](https://chakra-ui.com/), [Saas UI](https://saas-ui.dev) và TypeScript. Phù hợp làm trang marketing, giới thiệu sản phẩm SaaS với các khối nội dung (features, pricing, FAQ, testimonials) có thể chỉnh trong thư mục `data/`.

Gốc template tham khảo: [Saas UI Next.js landing](https://saas-ui.dev) — [demo gốc](https://saas-ui-nextjs-landing-page.netlify.app/).

## Công nghệ

| Thành phần | Ghi chú |
|------------|---------|
| Next.js 14 | App Router, API Routes (`app/api/`) |
| React 18 | Client components khi cần tương tác |
| TypeScript | Toàn bộ mã nguồn chính |
| Chakra UI + Saas UI | Giao diện và component thương mại |
| Tailwind CSS + Sass | Style bổ sung (`globals.css`, `public/styles/`) |
| Redux Toolkit | Trạng thái form / flow (`app/store/`) |
| Framer Motion | Hiệu ứng chuyển động |
| Axios, crypto-js | Gọi API và mã hóa phía client/server tùy luồng |

Trong bản **production**, `next.config.mjs` có cấu hình **webpack-obfuscator** cho bundle phía client (làm rối mã, tắt `console` trên build release). Khi phát triển (`next dev`) obfuscation **không** bật.

## Yêu cầu môi trường

- **Node.js** 18 trở lên (khuyến nghị LTS, tương thích Next.js 14).
- **npm** (dự án có `package-lock.json`). Có thể dùng `pnpm`/`yarn` nếu bạn tự quản lý lockfile.

## Cài đặt và chạy

```bash
cd Code_link
npm install
```

Sao chép file môi trường mẫu và điền giá trị thật (đặc biệt Telegram nếu dùng API thông báo):

```bash
copy .env.example .env
```

Trên macOS/Linux:

```bash
cp .env.example .env
```

Chạy máy chủ phát triển:

```bash
npm run dev
```

Mở [http://localhost:3000](http://localhost:3000) trong trình duyệt (cổng mặc định 3000).

### Các lệnh npm

| Lệnh | Mô tả |
|------|--------|
| `npm run dev` | Chạy Next.js ở chế độ development |
| `npm run build` | Build production |
| `npm run start` | Chạy bản build (sau `build`) |
| `npm run lint` | ESLint (`next lint`) |

## Biến môi trường

Các biến trong `.env.example` — **không** commit file `.env` chứa bí mật thật.

| Biến | Mô tả |
|------|--------|
| `NEXT_PUBLIC_SETTING_TIME` | Thời gian đếm ngược (giây), mặc định logic code thường dùng `30` nếu không set. Dùng cho luồng modal (ví dụ 2FA). |
| `TELEGRAM_BOT_TOKEN` | Token bot Telegram (server) — dùng khi gửi thông báo qua helper `helper/telegram.ts`. |
| `TELEGRAM_CHAT_ID` | ID chat/kênh nhận tin từ bot. |

Các biến khác trong `.env.example` (ví dụ `NOTIFICATION_*`, `WEBHOOK_URL`, `NEXT_PUBLIC_NOTIFICATION_ENABLED`) có thể dành cho mở rộng sau; nếu không thấy được import trong mã nguồn, bạn có thể bỏ qua hoặc nối thêm logic theo nhu cầu.

## Chỉnh nội dung & SEO

- **Cấu hình site, header, footer, SEO mặc định:** `data/config.tsx`
- **Pricing, FAQ, testimonials, logo:** các file trong `data/` (ví dụ `pricing.tsx`, `faq.tsx`, `testimonials.tsx`)
- **Trang chủ marketing:** `app/(marketing)/page.tsx` và `app/(marketing)/layout.tsx`
- **Trang động theo slug:** `app/[slug]/`
- **Metadata / privacy center:** `app/metadata/`, `app/privacy-center/`

Sửa file tương ứng rồi lưu; dev server sẽ hot-reload.

## API nội bộ (tóm tắt)

- `app/api/privacy-center/route.ts` — xử lý POST, giải mã payload (AES) và có thể gửi tin qua Telegram tùy cấu hình.
- `app/api/ip-location/route.ts` — phục vụ thông tin vị trí/IP theo luồng ứng dụng.

Chi tiết luồng xem mã trong từng `route.ts`.

## Triển khai

Có thể triển khai lên [Vercel](https://vercel.com), Netlify hoặc bất kỳ host nào hỗ trợ Node cho Next.js. Thêm biến môi trường trên dashboard host giống `.env` local.

Tài liệu Next.js: [Deployment](https://nextjs.org/docs/app/building-your-application/deploying).

## Tài liệu tham khảo

- [Saas UI Docs](https://saas-ui.dev/docs)
- [Next.js Documentation](https://nextjs.org/docs)
- [Chakra UI](https://chakra-ui.com/docs)

## Giấy phép

MIT
