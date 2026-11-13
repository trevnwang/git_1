# Web UI (M0)

Vue 3 + TypeScript + Vite。构建产物被 WPF Host 通过 WebView2 虚拟主机（app.local）加载：

`powershell
npm install
npm run build     # 输出 ui/dist，Host 构建时复制到输出目录 ui/
`

- 设计 token：src/styles/tokens.css（SRS §11.2 双通道色板，含深色）。
- App Shell / 页面：src/App.vue（看板/组件/CI/任务/通知/日志/设置；CI 10 锚点 + Mock 数据）。
- UI 自检记录：../docs/ui-review-m0.md。
