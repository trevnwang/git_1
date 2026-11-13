# v3 工作台实现 · 对抗式代码评审报告（.NET + 前端安全/性能）

| 项目 | 内容 |
|---|---|
| 评审对象 | 后端 `src/MbdGitTool.Core/{Contracts,Security,Files,Domain}/**`、`src/MbdGitTool.Host/{Bridge,Services}/**`、`MainWindow.xaml.cs`；前端 `ui/src/{api.ts,stores/workbench.ts,components/**,composables/**,mocks/**}` |
| 对标方案 | `docs/ui-redesign-code-plan.md`（下称 code-plan）、`docs/ui-redesign-spec.md`（下称 spec）、`reqs/requirements.md` §10.2 AC-UI-01…34 |
| 只读证据 | 运行中 Host + WebView2 CDP 9222 真机探针（`Runtime.evaluate` 直发桥消息、真实 DOM 读取；未执行任何写文件/openPath 真实启动） |
| 运行态 | `source=local+gitea`，workspace=`D:\Workspace\git_1\artifacts\real-test\workspace`，4 组件 / 14 仓；页面 `https://app.local/index.html`，构建产物 `bin/Debug/net10.0-windows/ui/assets/index-CEGxHDqY.js`（2026-11-14 02:55:11） |
| 基线 | `dotnet test MbdGitTool.sln -c Debug`：**Core 71 passed / Host 145 passed / 0 failed / 0 skipped**（5s） |
| 边界 | 只新建本报告，不改代码；未执行任何危险扩展名 openPath（避免真实启动程序），相关攻击以“拦截 postMessage + 代码路径”双重取证；未创建 junction 夹具（副作用会改工作区，reparse 结论给可执行复现步骤与代码行） |
| 日期 | 2026-11-14 |

---

## 0. 结论摘要（5 分钟版）

**一句话：路径与桥的“安全主链路”实现得比方案要求更严（未发现可越出 workspace 的 PoC，安全 P0=0）；但前端在真实 Host 模式下存在 1 个核心功能阻断缺陷（文件夹视图懒加载完全失效），另有 10 个 P1（错误信息被静默吞掉、超时预算设计错误、超限请求无应答、大仓树加载必超时、危险扩展名确认无服务端凭据、reparse 残余缺口、快照 2MB 扩容瓶颈、AC 未覆盖等）。当前不适合直接进入 M1 出口/AC 签收，建议先修 P0+P1 前 4 项再跑真实环境回归。**

| 口径 | 结果 |
|---|---|
| 安全 P0（可越权/可 RCE 的可利用链） | **0**（31 组恶意路径、危险扩展名、UNC/ADS/`.git`/reparse/超限/参数类型全部按预期拒绝；token 未在错误/日志中泄露） |
| 功能 P0（AC 阻断） | **1**：文件夹视图展开仓/目录不发请求、不加载子节点 → **AC-UI-14 直接失败**（可复现，见 P0-1） |
| P1 | **10 条**（见 §2；其中 6 条可复现，4 条为可量化理论风险） |
| P2 | 17 条（见 §3） |
| Top10 必须修 | 见 §7 |
| 是否需要先修再继续 | **是**。至少先修 P0-1、P1-1、P1-2、P1-3/P1-4 后再进行真实 Host 的 AC-UI 全量回归；P1-5/P1-6/P1-9 为安全/健壮性，需在 M1 出口前完成。 |

**与既有需求侧评审的关系**：`docs/review-req-v3.md` 已从 FR/AC 角度列出“需求未落地/自相矛盾”；本报告只从**实现代码与运行行为**给出可执行缺陷、行号、攻击/复现与修复代码，两者互补。

---

## 1. 评审方法与证据说明

1. **静态核对**：逐文件读后端 PathPolicy/FileTreeReader/RepoTreeService/WorkspaceTreeService/OpenPathService/GitStatusIndex/CommitsService/HostCommandRouter/WebViewBridge，与前端 api/store/viewmodel/组件逐行对照 code-plan/spec。
2. **运行探针（CDP 9222，只读）**：
   - 直接 `window.chrome.webview.postMessage` 发 31 组恶意 path/params，记录真实回包（不经过前端 normalize，可验证 Host 边界）；
   - 真实鼠标事件（`Input.dispatchMouseEvent`）触发文件夹视图展开，并 **拦截** `postMessage` 记录 UI 实际发出的 `getWorkspaceTree` / `openPath` 参数（openPath 一律拦截不转发，确保不启动任何程序）；
   - 读取运行中 bundle 源码片段，确认“运行态 = 被评审源码”（如 `loaded:e.children!==null&&e.children!==void 0||r.length>0`）。
3. **测试基线**：`dotnet test` 全绿（0 failed/0 skipped），说明下述缺陷**均不在现有单测覆盖范围内**（测试缺口见 §6）。

> 严重度：**P0**=阻断 AC/安全可利用/数据损坏；**P1**=核心路径错误、可复现的体验/性能/安全硬化缺陷；**P2**=一致性问题、理论风险、维护性。
> 状态：**【可复现】**=本机实测或确定性的代码路径；**【理论】**=需特定夹具/本地写权限/规模条件，已给复现配方。

---

## 2. P0 / P1 缺陷清单（含文件:行、证据、复现、修复）

### P0-1 文件夹视图“展开仓库/目录”完全失效，子节点永不加载（AC-UI-14 失败）【可复现】

- **位置**：`ui/src/api.ts:739` + `ui/src/api.ts:766-768`、`ui/src/viewmodel.ts:397-440`（关键行 `viewmodel.ts:437`）、`ui/src/stores/workbench.ts:1185-1227`（`if (!node.loaded)` 在 1196）。
- **根因**：`normalizeWorkspaceNode` 把缺失的 `children` 一律归一为 `[]`（`const children = readArray(record,'children') ?? []`，随后 `children: children.map(...)`），于是 viewmodel 中 `loaded: (dto.children !== null && dto.children !== undefined) || repos.length > 0` 对**所有** Host 真实节点恒为 `true`；`toggleFolderNode` 看到 `node.loaded===true` 就跳过 `getWorkspaceTree`，直接把空 `children` 展开。
  运行中 bundle 已确认同一逻辑：`loaded:e.children!==null&&e.children!==void 0||r.length>0`（`ui/assets/index-CEGxHDqY.js`）。
- **证据（CDP 实测，真实 Host 模式）**：
  - 进入文件夹视图后根层 `4 节点`；展开 COMPONENT 后 `8 节点`（4 组件 + 4 仓，符合“组件组内联不发请求”）；
  - 再点 REPO 的展开箭头（`aria-expanded` 已变 `▾`）后仍 `8 节点`；
  - 全程 `window.__reqs` 中 **`getWorkspaceTree` 只有一次**（`path:""`），**0 次**子路径请求；
  - 因此文件夹视图内 FILE 节点永远不可达，右侧“文件信息/用默认程序打开/打开所在目录”分支全部死代码。
- **为何单测/自测没发现**：`ui/scripts/cdp-selfcheck.mjs:457-478` 是 **Mock 模式**（等待 7 张 Mock 卡片），Mock 根节点不带 `children` 字段 → `loaded=false` → Mock 下展开正常；真实 Host 才触发。该脚本也只点第一个 `workspace-toggle`（组件组，内联展开），从未点 REPO 节点。
- **修复**（三处一起改，保持“未加载”与“空目录”可区分）：
  ```ts
  // 1) api.ts：保留“未提供 children”语义
  export interface WorkspaceNodeDto { /* ... */ children: WorkspaceNodeDto[] | null }
  const children = readArray(record, 'children')
  // ...
  children: children ? children.map(normalizeWorkspaceNode).filter((n): n is WorkspaceNodeDto => n !== null) : null,

  // 2) viewmodel.ts mapWorkspaceNode：distinguish null
  const rawChildren = dto.children            // null = 未加载
  // ...
  loaded: rawChildren !== null || repos.length > 0,
  children: (rawChildren ?? []).map((c) => mapWorkspaceNode(c, depth + 1)),

  // 3)（可选）mock 返回 children:null 保持一致
  ```
- **验收**：真实 Host 下新增 CDP 断言——展开 REPO 后：`getWorkspaceTree` 子路径请求恰 1 次、`workspace-node` 数增加；折叠/再展开 0 新请求（AC-UI-14 原文要求）。

---

### P1-1 树读取的告警/错误字段被前端整体丢弃：读取失败、reparse、截断都被显示成“（空）”【可复现】

- **位置**：Host 产出：`RepoTreeService.cs:179-183`（`Warnings[0]` → `RepoTreeDto.Error`）、`FileTreeReader.cs:42-49/89-103/100-103`；前端丢弃：`ui/src/stores/workbench.ts:613-627`（`applyTree` 固定 `panel.error=''`，从不读 `dto.error`）、`:706-710`（`toggleTreePath` 展开同样忽略 `dto.error`）、`:1106-1117`（`ensureFolderLoaded` 忽略 `WorkspaceTreeDto.error`）、`:1212-1217`（`toggleFolderNode` 同样）；展示层 `RepoTreePanel.vue:125-165`、`WorkspaceTree.vue:217-236` 也没有 warning 行。
- **证据（CDP 实测）**：
  - `getRepoTree {componentId:'RTCT_Demo',repoType:'IMPL',path:'no-such-dir'}` 返回 **`ok:true`, `entries:[]`, `error:"E_IO_ERROR"`**（Host 端的“降级”语义）；
  - 前端 `applyTree` 把 status 置 `ready`、error 置空 → 面板显示“空目录/（空）”，用户无法区分“真空目录 / 目录被删 / 无权限 / reparse 被阻止 / 超过 20000 扫描保护”；
  - `E_ACCESS_DENIED`、`E_REPARSE_POINT`、`E_TOO_MANY_ENTRIES` 同理被静默；spec §4.5 要求 `tree-truncated` 警告行、§4.2 要求 reparse 节点 `⟲` 标记，均无法满足。
- **修复**：
  ```ts
  // workbench.ts applyTree
  panel.errorCode = dto.error ?? ''
  panel.error = dto.error === 'E_ACCESS_DENIED' ? '没有权限读取该目录'
              : dto.error === 'E_REPARSE_POINT' ? '符号链接/junction 不允许展开'
              : dto.error === 'E_TOO_MANY_ENTRIES' ? '目录超过 20000 项保护上限' : ''
  panel.status = dto.error && dto.error !== 'E_TOO_MANY_ENTRIES' ? 'error' : 'ready'
  // toggleTreePath：把 dto.error 写到 node.error 并 toast；RepoTrePanel 增加 warning 行
  ```
  建议 Host 侧把 `Warnings: string[]` 原样下发（当前仅 `Warnings[0]` 丢失其余），并让“目录不存在”在 Host 直接 `E_PATH_INVALID`（与 `getWorkspaceTree` 的 `BuildExpansionAsync:252-270` 行为对齐，当前两 API 不一致）。
- **影响**：这是“静默失败”类正确性问题，会让用户误以为仓库是空的（尤其网络盘/OneDrive/权限受限目录），也掩盖了 Host 的安全拦截。

---

### P1-2 真实 Host 返回空组件列表时，前端伪造一张 Mock 组件卡片【代码级可证】

- **位置**：`ui/src/stores/workbench.ts:432-438`（`else if (state.components.length === 0) state.components = [mapComponent(mock.mockSnapshot().components[0])]`），状态 `snapshotEmpty` 在 433/451 已算好却没有用于渲染。
- **证据/路径**：`SnapshotService.CreateSnapshot`（`SnapshotService.cs:385-415`）在“工作区存在但没有任何合法仓”时返回 `Components=[]`、`Source="local"`（`AssembleLocal` 输入为空，`SnapshotService.cs:216-226`），这是首次配置错误目录的常见场景；前端 `applySnapshot` 随即注入 `CF325F_ChanganSigProcgBascFct` 假组件，卡片可点，随后 `getRepoTree` 返回 `E_NOT_FOUND`/`E_REPO_NOT_LOCAL`，树面板报错。违反 spec §8.4“真实数据保留规则”、AC-UI-33（空态必须可见说明 + 重试入口）。
- **修复**：
  ```ts
  state.snapshotEmpty = components.length === 0
  if (!state.snapshotEmpty) state.components = components
  else state.components = []       // 渲染 EmptyState（已有 ComponentList.vue:183-192 分支）
  // 仅当 snapshot.source === 'mock'（Host 明确声明无工作区）时才允许展示 Mock 数据
  ```
  同时 `isMockMode()` 不应由“顺手回退”决定，而应显式 `source==='mock'`。

---

### P1-3 大仓 `getRepoTree` 与路由超时共用 10s 预算，且 git status 失败不降级 → 树加载必超时【理论，大仓/冷缓存必现】

- **位置**：`HostCommandRouter.cs:38`（getRepoTree 10s）、`RepoTreeService.cs:105`（先 `await GetSubmodulesAsync`）、`:136-139`（再 `await GetStatusAsync`，最后才 `_reader.Read`）、`GitStatusIndex.cs:31`（status 自身 10s）；code-plan §4.4.3（`docs/ui-redesign-code-plan.md:848-857`）明确要求“若超时，返回 gitStatus=NONE + warning，不阻塞树”，实现未做。
- **机制**：`git status --porcelain=v2 -z --untracked-files=all` 在大仓（>5 万文件、冷缓存、网络盘、杀软）可 >10s；树请求总超时也是 10s，且 status 在枚举**之前**串行执行 → 路由 `CancelAfter(10s)` 先触发，`OperationCanceledException` → `E_TIMEOUT`；树此前一次都没枚举。`toggleTreePath` 每个展开还会复用同一逻辑（缓存 30s 后才快）。
- **复现配方**：夹具仓放入 `node_modules/` 或 10 万小文件（或用慢盘/Defender 实时扫描），首次进入工作台后展开该仓 → 面板 `E_TIMEOUT`，`HostLog` 见 `api getRepoTree ... timeout`；同时 `git status` 进程被 kill。
- **修复**：
  ```csharp
  // RepoTreeService.GetTreeAsync
  var page = _reader.Read(...);                              // 1) 先枚举（可取消、毫秒级）
  if (includeGitStatus) {
      using var statusCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
      statusCts.CancelAfter(TimeSpan.FromSeconds(2));         // 2) 独立短预算
      try { statuses = await _annotations.GetStatusAsync(repoRoot, statusCts.Token); }
      catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
      { statuses = Empty; page = page with { Warnings = page.Warnings.Append("E_STATUS_TIMEOUT").ToArray() }; }
  }
  // 3) 子模块读取失败同样降级为空集，不阻塞
  ```
  并把 `GetSubmodulesAsync` 与 `Read` 并行/后置；§4.13 目标“4 仓首次并发 ≤1.5s”才能成立。

---

### P1-4 前端超时预算 = Host 超时预算（缺 code-plan §3.12 要求的 +5s 余量），临界请求必现假超时【可复现/设计缺陷】

- **位置**：`ui/src/api.ts:232-244`（`TIMEOUTS` 与 Host 完全相同：snapshot 45s、refresh 120s、tree 10s、openPath 8s…）对照 `HostCommandRouter.cs:30-44`；code-plan `docs/ui-redesign-code-plan.md:612-621` 第 618 行明确“UI 每请求 | Host 超时 + 5s 余量”。
- **机制**：Host 在 T 秒取消并回 `E_TIMEOUT` 信封，前端在 T 秒同时 reject，二者竞态；大仓 status/冷启动/大组织 Gitea 同步只要接近 T 秒，用户看到的是前端 `E_TIMEOUT`（“Host 可能未就绪”），而非 Host 的真实错误码；`refresh` 双方同为 120s，Host 端取消与前端取消不可区分，重试语义丢失。
- **修复**：`TIMEOUTS[m] = HOST_TIMEOUTS[m] + 5_000`（openPath/ci 等短请求至少 +2s）；建议由 Host `getHostInfo.Limits` 下发超时表，避免双份硬编码。

---

### P1-5 请求体/响应体上限实现有洞：按“字符”而非 UTF-8 字节；超限/坏 JSON 回 `id:null` 导致前端永远收不到错误（静默挂起到自身超时）；响应大小在序列化后才检查【可复现】

- **位置**：`WebViewBridge.cs:94-103`（`rawJson.Length > MaxRequestBodyBytes` → `PostResultAsync(null, E_INVALID_REQUEST)`）、`:122-125`（id 无类型校验）、`:162-208`（`json.Length > MaxResponseBytes` 在 `JsonSerializer.Serialize` 之后）、`:230-240`；前端 `api.ts:353-374`（`onBridgeMessage` 只对“无 id 且形如 snapshot”的载荷做兜底），`api.ts:376-391`。
- **证据（CDP 实测）**：发送 70,000 字符的 `getHostInfo` 请求 → **6s 内没有任何可配对应答**（探针超时）；Host 实际回了 `{id:null, ok:false, code:E_INVALID_REQUEST}`，前端 pending map 按 id 匹配不到、`looksLikeSnapshot` 也不成立 → 被丢弃。对照：非对象 JSON → `E_INVALID_REQUEST`（同样 id:null）；数组 params → `E_INVALID_ARGUMENT` 正常；`depth:"abc"` → `E_INVALID_ARGUMENT` 且 `details.field="$.depth"`。
- **风险**：① 64KB 限制按 UTF-16 chars 计，中文名/长中文消息实际可到 ~192KB UTF-8；② 恶意/异常客户端可制造“桥不回应”状态（UI 只能等 10-45s 超时）；③ 2MB 响应上限按 chars 计，实际 UTF-8 可达 6MB，可能触及 WebView2/Chromium WebMessage 传输上限（未在本机验证到上限，标为理论）；④ `depth=2` 大树在 `Serialize` 阶段已把数百 MB 字符串分配出来，**上限检查太晚**，存在 OOM 风险（见 P2-4）。
- **修复**：
  ```csharp
  // 1) 字节口径 + 尽量回显 id（先轻量提取 "id" 字段再判大小）
  var byteLen = Encoding.UTF8.GetByteCount(rawJson);
  if (byteLen > MaxRequestBodyBytes) { await PostResultAsync(TryExtractId(rawJson), Failure(...)); return; }
  // 2) 在序列化前用业务上限兜底（entries 数、depth、take、组件数），超限直接 E_RESPONSE_TOO_LARGE
  // 3) id 校验：只接受 string/number，否则 E_INVALID_REQUEST（同时回 id:null）
  ```
  前端 `onBridgeMessage`：若 `id===null && reply.ok===false`，应 reject **一个** pending 请求（或统一 toast），不能静默丢弃。

---

### P1-6 `openPath` 危险扩展名“二次确认”由调用方自证（`confirm:true`），Host 无一次性凭据/无审计绑定【可复现机制，需页面可执行脚本或本地进程】

- **位置**：`OpenPathService.cs:245-270`（`extensionClass is not (Directory or SafeText) && parameters.Confirm != true` → `E_CONFIRM_REQUIRED`；`confirm=true` 直接 `ProcessStartInfo{FileName=canonical, UseShellExecute=true, Verb="open"}`）、`BridgeParams.cs:44-50`、`ui/src/stores/workbench.ts:839-909`。
- **攻击输入**（任意可向桥发消息的脚本；当前页面即 app.local 可信源，故列为信任模型缺陷而非直接漏洞）：
  ```js
  window.chrome.webview.postMessage({ id:'x', method:'openPath',
    params:{ componentId:'RTCT_Demo', repoType:'IMPL',
             path:'tools/build.ps1', mode:'open', confirm:true }});
  ```
  → Host 直接执行/打开该危险文件；`.bat/.ps1/.exe/.lnk/.url` 均适用。若前端未来出现 XSS（如把提交信息/远端字段以 `v-html` 渲染、引入第三方库漏洞），或 Host 以 CDP/DevTools 调试模式运行（见 P2-11），该路径即升级为“网页内容 → 任意程序执行”。
- **已验证的防线**：路径仍被夹在工作区内、`.git`/`.trash`/submodule/reparse 文件/危险扩展名未确认都被拒绝（31 组恶意路径全部 `E_PATH_*`，见 §4）；因此攻击者不能借 confirm 越出 workspace，但**可在工作区内执行任意脚本/可执行文件**（若工作区存在或攻击者可写入）。
- **修复**：改为服务端确认令牌，而不是布尔值：
  ```csharp
  // 第一次：返回 E_CONFIRM_REQUIRED, details.confirmToken = HMAC(path|ext|exp|nonce), exp=60s
  // 第二次：confirmToken 必须匹配且一次性消费；Host 记录审计日志（相对路径+扩展名+时间）
  // 另加：mode=open 时若进程不在前台用户手势窗口内（可选记录），或对 DANGEROUS 类固定走 reveal
  ```

---

### P1-7 提交 Tab 无 epoch 防护：切换组件后旧请求把别的仓的提交追加到新组件【代码级可复现】

- **位置**：`ui/src/stores/workbench.ts:783-824`（`loadCommits` 捕获 `component/repo` 后 await，回来直接写 `state.commits.total/items.concat(...)`，无 `epoch`/组件校验）；对照 `loadTree`/`loadDetail` 都有 `currentEpoch !== epoch` 丢弃（`:652-675`、`:751-775`），`selectComponent` 在 `:598` 重置 `state.commits` 但未使 in-flight 请求失效。
- **复现**：选中组件 A → 打开“提交”Tab（`getCommits` 未返回）→ 立刻点组件 B 卡片 → A 的响应返回后 `state.commits.items` 被拼进 B 的提交列表；随后“加载更多”的 `skip` 也基于污染后的长度。
- **修复**：
  ```ts
  async function loadCommits(reset = false) {
    const epochAtStart = epoch; const compId = component.id; const repoType = state.focusedRepoType
    ...
    if (epochAtStart !== epoch || compId !== state.selectedComponentId) return
    if (state.focusedRepoType !== repoType) return
    state.commits.total = page.total; ...
  }
  ```

---

### P1-8 快照 2MB 硬上限 vs 大组织：约 930 个组件即触顶，UI 直接整体回退 Mock 且不会自动恢复【量化理论风险】

- **位置**：`HostCommandRouter.cs:26`（`MaxResponseBytes = 2*1024*1024`）、`WebViewBridge.cs:193-205`；前端 `workbench.ts:487-518`（`loadInitialSnapshot` 失败 → `applyMockSnapshot` + 2 次重试后放弃）、`api.ts` 无“自动降 limit 重试”（code-plan §3.11 要求）。
- **量化证据**：当前真实快照 `JSON.stringify(data).length = 8,631 chars`（4 组件/14 仓，`source=local+gitea`）→ **≈2.16KB/组件**；2,000,000 / 2,160 ≈ **926 组件**即触顶。spec/code-plan 风险表明确写到“4000+ 组件快照 1.5–3MB，需 getComponentPage 分页”（`docs/ui-redesign-code-plan.md:1029-1031`），而 **`getComponentPage` 未实现、契约未落地**。
- **影响**：大组织（数百~数千组件）首次进入即 `E_RESPONSE_TOO_LARGE` → 静默展示 Mock 假数据（叠加 P1-2 的“伪造”问题），无任何提示，用户会误以为 Host 不可用。
- **修复（按代价递增）**：① 为 `getSnapshot` 单独放宽上限/允许分块（WebView2 `PostWebMessageAsJson` 大消息风险需实测）；② 落地契约预留的 `getComponentPage(offset,limit)`，首屏只下发聚合头 + 前 N 组件；③ UI 对 `E_RESPONSE_TOO_LARGE` 显示明确错误 + 重试，禁止静默 Mock。
- **验收**：造 400/1000 组件夹具，测 `getSnapshot` 字节数与首屏时间；≥1000 组件不得回退 Mock。

---

### P1-9 reparse 防护不一致：`getRepoTree` 根层（path="")不检查、`openPath` 目标为 junction 目录时放行、且检查与使用之间存在 TOCTOU【理论 + 本地可复现】

- **位置**：`RepoTreeService.cs:114-124`（仅 `relativePath.Length > 0` 才做 `CheckReparseChain`/`IsReparsePoint`；`path=""` 即仓根本身完全不检查，直接 `_reader.Read(repoRoot…)`）；`OpenPathService.cs:180-195`（祖先 reparse 拒绝，但 `isReparseTarget && mode=="open" && !isDirectory` 只拦**文件**；目录 junction 在 open/reveal 都放行）；`PathPolicy.cs:93-130`（`CheckReparseChain` 只做 `File.GetAttributes` 探测，不解析链接最终目标；且 `:101-104` 对“越出 Root 的路径”返回 `ReparseCheck.None` 而不是“非法”）。
- **复现配方（本地可复现；需对本机工作区有写权限，属“快照被替换”威胁）**：
  ```bat
  :: 1) 先用真实工作区生成快照（正常）；2) 替换某个仓目录为 junction：
  rmdir /s /q "%WS%\RTCT_Demo_Impl"
  cmd /c mklink /J "%WS%\RTCT_Demo_Impl" C:\Windows\System32
  :: 3) 不刷新快照，直接请求：
  ```
  ```js
  // CDP 实测等价请求（本次未创建 junction，故标理论）
  window.chrome.webview.postMessage({id:'t', method:'getRepoTree',
    params:{componentId:'RTCT_Demo', repoType:'IMPL', path:''}});
  // 预期（代码路径确定）：成功列出 C:\Windows\System32（因为 path=="" 跳过 reparse 检查）
  // 对照：path:"win.ini" 或 getWorkspaceTree{path:"RTCT_Demo_Impl"} 才会被 E_REPARSE_POINT 拒绝
  ```
  `openPath`：`mklink /J "%WS%\link" C:\Windows` 后 `openPath{path:"link",mode:"open"}` → `explorer.exe C:\Windows`（目录目标不拦）；`mode:"reveal"` 同。
- **修复**：在**任何**读/开路径上统一执行 `CheckReparseChain(full, includeTarget: true)`，并把检查提前到 `path==""` 分支之前；`PathPolicy` 增加“解析最终目标并 IsWithin”的能力（`File.OpenHandle` + `Path.GetFinalPathNameByHandle`，或 `FileSystemInfo.LinkTarget` 递归解析），TOCTOU 残余风险在报告中显式记录（ShellExecute 按路径打开，无法完全消除，可改为先解析最终路径再打开并对结果复核）。
- **说明**：这不是当前可直接利用的远程漏洞（本地扫描器 `LocalScanner.cs:141-146` 会跳过 reparse 仓并不纳管，快照中的 repo 路径正常为真实目录；需要“先扫描、后被替换”或本地写权限），故定 P1 硬化而非 P0。

---

### P1-10 AC-UI-19 批量确认只对 DELETE 生效，SYNC/CREATE 11 组件不弹确认框【可复现】

- **位置**：`ui/src/stores/workbench.ts:1001-1019`，第 1004 行 `if ((count > 10 || repoCount > 40) && command === 'DELETE')`。
- **对照**：spec `docs/ui-redesign-spec.md:905`（AC-UI-19）明确“选 3 个组件点 SYNC → 无确认；选 11 个再点 SYNC → `confirm-dialog` 出现且含‘组件 11 / 仓 40+’”。当前 SYNC/CREATE 走 `notify('批量 SYNC…M2 接入')`，永远不确认。
- **修复**：
  ```ts
  const needConfirm = ['SYNC','CREATE','DELETE','RESTORE'].includes(command) && (count > 10 || repoCount > 40)
  ```
  并补 `confirm-dialog` 文案断言（`组件 ${count} / ${repoCount} 个仓`）。

---

## 3. P2 清单（17 条，含位置与建议）

| # | 问题 | 位置/证据 | 修复建议 |
|---|---|---|---|
| P2-1 | “在资源管理器中打开组件目录”对 MISSING 组件给出期望路径（不存在）或 `.`，必失败 | `workbench.ts:1325-1345`（`repo?.localPath ?? component.repos.find(item=>item.localPath)?.localPath ?? state.workspaceRoot`；`path = relative || '.'`）；CDP 实测远程组件 → `openPath{path:"RTCT_RemoteOnly_Design",mode:"reveal"}`；无本地仓时 → `path:"."` 被 PathPolicy 拒 `E_PATH_INVALID` | 只用 `isLocal===true` 的 localPath；工作区根用 `path:""`（当前 `openPath` 以“缺 path”拒绝，建议支持 `path:""`=root 或改用 reveal 祖先目录）；`'.'` 一律不用 |
| P2-2 | 前后端打开白名单不一致：`.cs/.props/.targets/.gitattributes/.editorconfig` 后端 SAFE_TEXT、前端未知 → 多一次确认 | `FileExtensionPolicy.cs:20-24` vs `viewmodel.ts:264-282` | 扩展名策略单一事实源（`getHostInfo` 下发或 `Classify` API），前端删除硬编码表 |
| P2-3 | `getRepoTree` 对不存在子目录返回 `ok:true`+空列表，`getWorkspaceTree` 则 `E_PATH_INVALID`：两 API 语义不一致 | `RepoTreeService.cs:179-183` + `FileTreeReader.cs:42-49` vs `WorkspaceTreeService.cs:252-270` | 统一为“目录不存在 → 失败 E_PATH_INVALID”；权限/reparse → 成功+warning（前端按 P1-1 展示） |
| P2-4 | `depth=2` 可放大到 `2000×2000` 节点，无节点数上限；2MB 检查在序列化后（先分配再判） | `RepoTreeService.cs:99`、`FileTreeReader.cs:135-145`（children 递归 Read 用同一 limit）、`WebViewBridge.cs:193` | 增加“单响应节点上限”（如 8000）与 `depth=2` 的子目录数上限；序列化前用计数预判 |
| P2-5 | 缓存无 in-flight 去重：TTL 过期后同仓并发展开 N 个目录 → N 次 `git status`；`TtlCache` 满即整体清空 | `TtlCache.cs:43-51`、`GitStatusIndex.cs:46-81`、`RepoTreeService.cs:133-176` | 加 `Lazy<Task<...>>`/单飞；容量策略改 LRU/分片清理 |
| P2-6 | `includeHidden` 默认值漂移：`TreeReadOptions` 默认 `false`，两个 Service 却 `?? true`；UI 无法控制，OS Hidden/System（desktop.ini/Thumbs.db）默认出现 | `TreeReadOptions.cs:15`、`RepoTreeService.cs:101`、`WorkspaceTreeService.cs:69`、`api.ts:952-961`（不传 includeHidden） | 明确默认（建议 false，符合 code-plan 默认）；`getHostInfo` 暴露默认值；UI 菜单“显示隐藏文件”接上 |
| P2-7 | `GetWorkspaceTreeParams.Depth` 被完全忽略（恒 depth=1）；`GetRepoTreeParams.Cursor/NextCursor` 永远 null；`getComponentDetail`/`getCiStatus` 前端从不调用（死接口） | `WorkspaceTreeService.cs:46-129`、`TreeDto.cs:40`、`api.ts:219-231`（union 有、实现无 `getComponentDetail`）、`workbench.ts` 无调用 | 要么实现/校验，要么从契约删除；`Cursor` 标注 reserved |
| P2-8 | 可用性矩阵双份：后端 `RepoActionAvailabilityDto.Enabled=false`（M1 恒禁用）与前端本地矩阵（SYNCED 时 PULL enabled=true）互相矛盾；AC-UI-16 按矩阵要求 enabled | `RepoActionService.cs:39-81`、`viewmodel.ts:469-610`；前端从不消费 `actions` | 单一事实源：前端用 Host `actions`（或 Host 改 `Enabled` 语义为“矩阵可用”）；否则 M2 接入时必出 bug |
| P2-9 | UI 透明忽略 `TreeNodeDto/WorkspaceNodeDto.IsReparsePoint`（normalize 不保留），树中无法显示 `⟲` 链接标记、无法在 reveal 前提示 | `api.ts:684-707`、`:733-770`；spec §4.2 `docs/ui-redesign-spec.md:352-362` | 归一化保留 `isReparsePoint`，FileTree/WorkspaceTree 增加标记与 tooltip |
| P2-10 | `ignoreIgnored`/`compact` 为死状态：菜单“视图→显示已忽略文件”只弹 toast 不改变渲染；`includeIgnored` 前端从未发送 | `workbench.ts:1377-1383`、`:113-114`、`FileTree.vue` 无过滤；`api.ts` 不传 `includeIgnored` | 接通菜单状态到 API/渲染，或从菜单移除并标注 M2 |
| P2-11 | 安全硬化缺口：`AreDevToolsEnabled=true` 硬编码；CDP 9222/`MBD_E2E_OPEN_DRY_RUN` 依赖环境变量；无 CSP；无 `NavigationStarting/NewWindowRequested` 白名单 | `MainWindow.xaml.cs:63-64`、`ui/index.html`（无 CSP meta）、code-plan §7.3 启动方式（`docs/ui-redesign-code-plan.md:1547`） | Release 关闭 DevTools、`#if DEBUG` 门控 CDP；index.html 加 CSP（`default-src 'self'; connect-src 'self'`）；导航仅允许 `https://app.local`；dry-run 仅 Debug |
| P2-12 | `HostLog.Redact` 漏 JSON 形式密钥（`"token":"..."` 的引号打断正则）；`SnapshotService` 错误 `ex.Message` 可能带绝对路径下发 UI（违反“错误只回相对路径”） | `HostLog.cs:104-110`（正则 `token\s*[=:]\s*`）、`SnapshotService.cs:152/196` | 增加 JSON 键值/query 参数脱敏；错误统一映射为稳定 code + 相对路径 |
| P2-13 | `params` 反序列化异常把 .NET 类型名/行号透给 UI（信息泄露），且 `details.field` 是 `$.depth` 而非契约字段名 | `HostCommandRouter.cs:407-413`；CDP 实测回包 `The JSON value could not be converted to MbdGitTool.Core.Contracts.GetRepoTreeParams. Path: $.depth \| LineNu…` | 只回稳定 `E_INVALID_ARGUMENT` + `details.field` 取 `ex.Path` 去 `$.'` 前缀；不拼 `ex.Message` |
| P2-14 | 批内快速切换组件/刷新导致重复请求与状态覆盖：`refresh()` 里 `applySnapshot` 已触发 `loadActive`，随后 `resetCaches`+再次 `loadActive`；`selectComponent` 对同组件也全量重载并清 `selectedPath`；`selectFolderNode` `.then` 竞态 | `workbench.ts:532-571`、`:581-602`、`:1144-1183` | 合并为单一 `reload(epoch)`；refresh 只做一次 loadActive；`selectComponent` 同 id 时 no-op；`.then` 前校验 epoch/选择 |
| P2-15 | 组件 ID 直接拼进 CSS 选择器 `[data-component-id="${id}"]`（CDP 导航/聚焦用），宿主返回含特殊字符的 ID 时选择器异常（当前 Windows 目录名限制下多数字符安全，属加固） | `ComponentList.vue:62/71/79`、`workbench.ts` 无 | 用 `CSS.escape(id)` 或 `Array.find(el=>el.dataset.componentId===id)` |
| P2-16 | `RepoDetail` 每次聚焦 2 个 git 进程（`git log` + `git rev-list --count`），缓存仅 10s/64，快速切组件会打满 `_ioGate`；刷新大工作区时 5 个请求（4 树+详情）抢 4 槽 | `CommitsService.cs:33-98`、`RepoDetailService.cs:52-67`、`HostCommandRouter.cs:424-448` | 详情与提交共享缓存；`rev-list --count` 合并进 `git log -n 6` 判断 more；`_ioGate` 提到 6 或分级 |
| P2-17 | `MainWindow.xaml` 标题仍 “(M0 shell)”；Mock 模式日志/通知文案含 “D:\Work\legacy…” 等假绝对路径，易误导审计 | `MainWindow.xaml:5`、`ModalHost.vue:42-49` | 标题/假日志清理（不阻塞功能） |

---

## 4. 路径安全专项（P0 要求逐项核对）

### 4.1 结论

- **未发现可越出 workspace / 可读取任意系统文件的路径穿越 PoC（P0=0）**。31 组恶意输入在 `getRepoTree`、`getWorkspaceTree`、`openPath` 三条入口全部返回稳定错误码；`.git`/`.trash` 在读路径全禁；`openPath` 只做 `Process.Start`（open/reveal），M1 不存在 `getFileContent`/写接口（`runRepoAction` 实测返回 `E_NOT_IMPLEMENTED`），“只读打开”成立。
- 仍有 P1-9 的 reparse 一致性/TOCTOU 硬化缺口、P2 的设备名黑名单缺口（见 4.3），但均需本地写权限或特定夹具，不构成当前 P0。

### 4.2 已实测的拒绝矩阵（CDP 直发桥，非前端归一化后）

| 攻击输入（params.path） | 结果 |
|---|---|
| `..`、`../..`、`..\..\Windows\System32`、`sub/../../other`、`x/../../..` | `E_PATH_INVALID`（“路径包含空段/./..”） |
| `C:\Windows\...`、`C:Windows`、`\\127.0.0.1\c$\Windows`、`\\?\C:\Windows`、`\\.\C:`、`//server/share/x`、`/etc/passwd` | `E_PATH_OUTSIDE_ROOT` / `E_PATH_INVALID`（':'） |
| `a:b`、`file.txt:stream`（ADS） | `E_PATH_INVALID` |
| `.git/config`、`.GIT/HEAD`、`src/.git/objects`、`repo\.git` | `E_GIT_INTERNAL` |
| `.trash/x`、`.trash` | `E_PATH_INVALID`（“仅恢复流程可见”） |
| `CON`、`sub/NUL.txt`、`COM1`、`AUX.txt`、`PRN..txt`、`COM¹` | `E_PATH_INVALID`（保留设备名/非法字符） |
| `< > " \| ? *`、段首尾空格、`a.`、`...`、`a//b`、`\0`、>1024 字符 | `E_PATH_INVALID` |
| `src/`（尾随分隔符） | 树 API **接受**（`NormalizeRelative` 去尾 `/`）；`openPath` **拒绝** → 两 API 不一致（P2-3 同类） |
| `..\..\Windows\System32\notepad.exe` + `confirm:true`（openPath） | `E_PATH_INVALID`，未启动任何进程 |
| `C:\Windows\System32\...`、UNC、`.git/config`、`README.md:evil`、`CON`、`src/`、`mode:"launch"`、空 path（openPath） | 全部 `E_PATH_*`/`E_INVALID_ARGUMENT`，未启动进程 |
| 64KB 边界/70,000 字符请求 | 无配对应答（见 P1-5） |
| `getWorkspaceTree{path:'.'}`、`{path:'no/such/dir'}`、`{path:file}`、`{path:'repo/.git'}` | `E_PATH_INVALID` / `E_INVALID_ARGUMENT` / `E_GIT_INTERNAL`（符合预期） |

### 4.3 残余风险与修复要点

1. **reparse**：见 P1-9。`PathPolicy.CheckReparseChain` 仅“探测属性”，不解析链接最终目标；`includeTarget` 在 `getRepoTree` 根层与 `openPath` 目录目标处被绕过。修复：所有读/开路径统一 `includeTarget:true` + 最终路径解析后二次 `IsWithin`。
2. **设备名黑名单缺口（P2）**：`PathPolicy.IsReservedDeviceName`（`PathPolicy.cs:320-352`）覆盖 `CON/PRN/AUX/NUL/COM1-9/LPT1-9`，**未覆盖 `CONIN$`/`CONOUT$`/`CLOCK$`**。CDP 实测 `CONIN$`/`CONOUT$`/`CLOCK$` 作为段时被放行到文件系统探测，因当前目录下不存在而最终 `E_PATH_INVALID`（未挂起/未读设备，实测 ms 级返回）；但 Win32 对设备名的特殊解析是历史行为，建议补黑名单并加正则 `^(CONIN\$|CONOUT\$|CLOCK\$|COM[0-9]|LPT[0-9])$`。
3. **TOCTOU**：`File.GetAttributes` → `CheckReparseChain` → `Process.Start(fullPath)` 之间存在窗口；本地进程可替换目标。建议在 open 前用 `File.OpenHandle` 打开句柄取最终路径校验；残余风险记录在案（用户级工具，攻击者已具备本地写权限时威胁有限）。
4. **`openPath` 危险文件确认** 见 P1-6；`MBD_E2E_OPEN_DRY_RUN=1` 会使真实环境“标记成功但未启动”（`OpenPathService.cs:26-40`），Release 不应保留该旁路。
5. **错误/日志不泄露 token/绝对路径**：实测所有错误文案只含相对 path/扩展名/稳定 code；`AppConfig.ToString` 脱敏；“params 类型非法”泄露 .NET 类型名（P2-13）；`WorkspaceInvalid`/扫描失败可能带绝对路径（P2-12）。

---

## 5. 文件树 / 工作区树 · 性能与正确性专项

| 检查项 | 结论 | 证据/建议 |
|---|---|---|
| 单目录 2000 截断 | **正确** | `FileTreeReader.cs:107-118` + `FileTreeReaderTests.Read_TruncatesAtLimitAndReportsTotal` |
| 20000 扫描保护 | 正确（`MaxScanEntries`，超限加 `E_TOO_MANY_ENTRIES`） | `FileTreeReader.cs:17,76-80,100-103`；前端丢弃 warning（P1-1） |
| 懒加载深度 | Host 端正确（depth 1；depth 2 仅内联一层），但 UI 文件夹视图懒加载已坏（P0-1）；工作台四树正常 | `WorkspaceTreeService.cs:348-367`、`FileTreeReader.cs:135-145` |
| 排序 | Host：DIR 优先→`OrdinalIgnoreCase`→`Ordinal`；前端又用 `localeCompare('zh-CN')` 重排，中文/多语言下顺序可能不同（渲染确定性风险） | `FileTreeReader.cs:211-225` vs `viewmodel.ts:378-385`；建议前端不重排或仅稳定去重 |
| 缓存失效 | `refresh` 全清（`HostCommandRouter.cs:183-189`）正确；TTL 30s；**无 in-flight 去重**（P2-5）；`TtlCache` 满清空 | `TtlCache.cs:43-51` |
| 符号链接/junction | 枚举展示不递归（`FileTreeReader.cs:132-145`）；展开拒绝（`WorkspaceTreeService.cs:272-277`）；但 `getRepoTree` 根层不查（P1-9）、UI 无 `⟲` 标记（P2-9） |
| 4 树并发 | 正常：`loadActive` 并发 4×getRepoTree + 详情；`_ioGate(4)`；实测缓存命中 6 并发 5ms 返回；**冷缓存时与 git status 抢 10s 预算**（P1-3） | `workbench.ts:577`、`HostCommandRouter.cs:60,420-448` |
| 大目录内存 | 单页 2000 节点安全；`depth=2` 最坏 4M 节点且先序列化后判 2MB（P2-4）；`_pageCache 512 页` 最坏可达数百 MB（需 512 个大目录，概率低） | 建议单响应节点上限 + 预判 |
| WebMessage 大小 | 请求按 chars 判 64KB（非字节）、响应按 chars 判 2MB 且过晚；超限回包 id:null 前端丢弃（P1-5） | `WebViewBridge.cs:94-103,193-205` |
| 工作区根层 | 不枚举仓内部（正确）；根层截断/告警 UI 不展示（P1-1）；`WorkspaceTreeDto` 无 total/limit 字段 | `WorkspaceTreeService.cs:188-236` |
| `INVALID` 仓 | code-plan §3.6 要求“ok:true 返回目录内容 + 告警”，前端 `loadTree` 直接 unavailable（`workbench.ts:642-647`），契约/实现不一致 | 二选一并补测试 |
| 大工作区扫描 | `LocalScanner` 每仓 5~6 个 git 进程、并行 8（`LocalScanner.cs:64-96`）；400 仓约 2000+ 进程，冷盘/杀软下 `refresh` 120s 可能不够（P2 级，需真实大夹具实测） | 建议 `refresh` 分段进度 + 可取消保旧快照 |

---

## 6. 前端正确性专项

### 6.1 状态与竞态

| 项 | 结论 |
|---|---|
| 选中/树/详情联动 | `focusRepo→selectTreeNode→loadDetail` 基本正确；**提交列表无 epoch**（P1-7）；`selectComponent` 对同组件也全量重载并清空 `selectedPath`（P2-14） |
| 刷新竞态 | `refresh()` 内 `applySnapshot` 已触发一次 `loadActive`，随后 `resetCaches` + 再次 `loadActive`，重复请求；panel 引用被替换保证旧响应不污染新树（代码 `loadTree` 持有旧 `panel` 引用），但详情/提交共享状态仍有覆盖风险（P2-14） |
| 树展开态 | `applyTree` 清空 `expanded`（`workbench.ts:613-619`），30s 后刷新/切组件后展开态丢失；AC 未断言，属体验问题 |
| 文件夹视图 | 组件组展开正确（内联 repos）；**仓/目录展开失效**（P0-1）；筛选只作用于“已加载节点”（设计如此） |
| Mock 回退 | 无桥→Mock 正常；**Host 空组件→伪造 Mock**（P1-2）；**大快照→静默 Mock**（P1-8）；Mock 与 Host Mock 组件 ID 不同（Host `MOCK_*` vs 前端 `CF*`）导致 source=mock 时左栏与文件夹视图数据不一致（P2） |
| 虚拟滚动 | `useVirtualRows` 固定行高实现正确（实测 8 行 DOM）；注意 `watch(scroller)` 换节点时不重绑 ResizeObserver（`useVirtualRows.ts:72-79`），切换视图后 viewportH 可能短期为 0（自愈于 scroll/measure） |
| 按钮禁用/并发 | 单仓按钮按前端矩阵 enabled（与 AC 一致但与 Host DTO 矛盾，P2-8）；`state.action.pending` 防重入正确；刷新按钮未 `disabled` 但 `refresh()` 有 guard（等价） |
| openPath 参数 | **已验证正确**：仓树双击发 `{componentId, repoType, path:"component.txt", mode:"open", confirm:false}`（仓相对）；文件夹视图/`openInExplorer` 发工作区相对且不带 componentId；带 componentId 却传工作区相对路径会被夹到仓根 → `E_PATH_INVALID`（fail-closed）。反例：`openInExplorer` 对 MISSING 组件（P2-1） |
| XSS | **未发现可利用 sink**：全仓无 `v-html/innerHTML/eval/new Function/document.write`；提交信息/文件名/分支/tag/错误文案均走 `{{ }}`（Vue 默认转义）；无 `:href/:src` 绑定外部 URL；`normalizeCiStatus` 的 `latest` 直转未校验（未来 CI URL 渲染前需补）；建议加 CSP（P2-11）与 `CSS.escape`（P2-15） |
| 大列表渲染 | 组件卡/树均虚拟滚动；`sortComponents` 每次派生排序（4000 组件 O(n log n)/render，可接受） |

### 6.2 接口/契约对齐（前端）

- `OpenPathResultDto` 只读 `{opened,mode,path,errorCode,message}`，Host 实际回 `absolutePath/extension/extensionClass/isReparsePoint` → 前端丢弃，UI 无法提示“reparse 未展开/扩展名分级”（非阻塞）。
- `RepoTreeDto/WorkspaceNodeDto` 的 `error/isReparsePoint/truncated` 未消费（P1-1/P2-9）。
- `getComponentDetail/getCiStatus` 在 `RequestMethod/TIMEOUTS` 中存在但 `api` 无 `getComponentDetail` 实现、store 从不调用（P2-7）。
- `getHostInfo.Limits` 已下发但前端未用（未按 code-plan §3.11 在 `E_RESPONSE_TOO_LARGE` 时自动降 limit）。

---

## 7. 契约兼容 / 测试缺口 / 真实 Gitea 回归点

### 7.1 契约兼容（SnapshotDto 追加字段）

- `SnapshotDto/ComponentDto/RepoDto` 追加字段**全部为可选参数**（`SnapshotDto.cs:8-57`），`SnapshotContractTests` 覆盖旧 JSON 反序列化、旧位置构造、camelCase 输出 → **兼容成立**。
- 偏差：`RepoDto.LastCommit` 实际类型 `CommitDto`，code-plan §3.14 写 `LastCommitDto`（字段同构，仅文档漂移）；`RepoDetailResponse.Repo` 非空但前端接口写 `RepoDto|null`（无害）。
- 未做：code-plan §3.15 要求 `tests/contracts/bridge/*.json` golden 键集比对，**仓库无 `tests/contracts`**；新增字段/改名无 golden 门禁。
- `getHostInfo.Limits` 中 `MaxResponseBytes=2MB` 与“4000 组件快照 1.5–3MB”自相矛盾（P1-8）。

### 7.2 测试缺口（Host.Tests 145 条仍全绿，说明以下盲区）

| 盲区 | 证据 | 建议用例 |
|---|---|---|
| `WebViewBridge` 完全无单测（来源校验/请求体/信封/id 回显/超限/并发/销毁） | `tests/**` 无 bridge 测试；`IsAppLocalSource` 为 private | 抽出 `BridgeProtocol.TryParse/Envelope` 纯函数并测；`[Theory]` 覆盖 `https://app.local`、`http(s)://evil`、`app.local.evil.com`、空 Source、`file://`、userinfo 变体 |
| 请求/响应大小上限、id:null 行为 | 无 | 64KB 边界/中文 70KB/2MB+ 响应；断言回包 id 可配对 |
| 路由超时/并发闸 | 仅“预取消 token”测试（`HostCommandRouterTests.cs:156-166`），“真正慢调用触发 E_TIMEOUT”“ioGate 4 槽排队/取消后不泄漏”无测试 | 注入 fake slow service + `SemaphoreSlim` 断言 |
| reparse | `PathPolicyTests.cs:153-171`、`FileTreeReaderTests.cs:182-197`、`OpenPathServiceTests.cs:237-255` 在 `mklink` 失败时 **silent return**（计为 passed，`0 skipped` 是假象）；**仓根 path=""、目标目录 junction、workspace 根 junction、TOCTOU** 均未测 | 统一 `Assert.Skip`（xunit v3）或测试环境断言 mklink 能力；补 4 个场景 |
| 设备名/特殊名 | 无 `CONIN$`/`COM0`/`LPT0`/RTLO(U+202E)/`%2e%2e`/`GIT~1` 用例 | 补 `[Theory]`（本次实测全部安全，但需固化） |
| 树 API 边界 | `depth=0/2`、`limit` 夹紧、`MaxScanEntries=20000` 截断、`getWorkspaceTree` 的 `Depth` 被忽略、`truncated` 传播 | 补 Host 服务级用例 |
| 缓存失效/竞态 | refresh 后旧 page cache、TTL 过期、同 key 并发 stampede | 注入 `TimeProvider`/fake clock + 并发测试 |
| 契约 golden | 无 `tests/contracts/bridge/*.json` | 按 code-plan §3.15 建立 |
| 前端单测/E2E | `ui/package.json` 无 test/vitest；`ui/scripts/cdp-selfcheck.mjs`（未跟踪、无 npm script 接入）**只跑 Mock 模式**；文档引用的 `ui/e2e/run-ac.mjs` 不存在 | 至少把 selfcheck 接成 `npm run selfcheck`（真实 Host 模式 + Mock 模式双跑），补 AC-UI-14/27/28/29/31 断言（AC-14 当前必失败） |
| AC 无自动测试 | AC-UI-05/09/10/11/14/19/22/27/28/29/31/33 目前无真实 Host 断言（selfcheck 只覆盖 Mock 子集；且 **AC-14 已被 P0-1 破坏**） | 按 §6.2 的 AC 清单逐条补 CDP case，与 `ui/scripts` 一起入库 |
| 真实夹具烟测 | `RealWorkspaceSmokeTests.cs:61` 断言 `Source=="local"`（配置了 Gitea 环境变量即失败）；夹具缺失时 `return`（假绿） | 显式 Skip/环境变量开关；覆盖 Gitea 路径 |

### 7.3 真实 Gitea 环境回归点（不能只靠 Mock/本地夹具）

1. **快照来源**：`source=local+gitea`、`owner`、`giteaBaseUrl`、`freshness.remoteFetchAt`；Gitea 401/403/429 时 `source` 降级 `local` + `error` 可读（`SnapshotService.cs:168-199`）。
2. **owner 发现降级**：org → user → search（`GiteaClient.cs:81-117`）；search 结果 owner 精确过滤（`:143-168`）；PAT 只有 `write:repository` scope 时不能 403 崩。
3. **绑定语义**：远程缺仓 `MISSING`、远程多同名仓 `AMBIGUOUS`（`SnapshotService.cs:310-341`）、命名不可解析要暴露 WARN；`LocalScanner.OriginMatches` 对 https/ssh/scp-like origin 的 host/org/name 校验（真实 Gitea URL 若带端口/IP，需回归）。
4. **真实大仓树**：`git status` 耗时（P1-3）、2000 截断、中文/空格/长路径、`.gitmodules` submodule 拒绝。
5. **真实 openPath**：白名单直开/危险扩展名确认/reveal（`IProcessLauncher` 在真实环境无法注入 fake，需 `MBD_E2E_OPEN_DRY_RUN` 或独立 e2e launcher 通道；AC-UI-10/11 的“桥收到 openPath 参数”可通过 CDP 拦截 `postMessage` 断言，本报告已用此法验证）。
6. **大组织快照**：>930 组件（P1-8）必测；`refresh` 120s（大仓 Gitea 分页 200 页上限）。
7. **Token**：错误/日志/诊断包不含 token（`GiteaClient` 只放 header）；`HostLog.Redact` 补 JSON 形态（P2-12）。

---

## 8. Top10 必须修（按建议修复顺序）

| # | 缺陷 | 级别 | 修复要点 | 验收（可自动） |
|---|---|---|---|---|
| 1 | 文件夹视图懒加载失效（P0-1） | P0 | api 保留 `children:null` 语义 + viewmodel `loaded` 判定 + mock 对齐 | 真实 Host：展开 REPO 后子路径 `getWorkspaceTree` 恰 1 次、节点增加、二次展开 0 请求（AC-UI-14） |
| 2 | 树 error/warning 被丢弃（P1-1） | P1 | `applyTree/toggleTreePath/ensureFolderLoaded` 消费 `dto.error`；Host 下发完整 warnings | `getRepoTree path:'no-such-dir'` → 面板显示错误态而非“（空）” |
| 3 | 空组件伪造 Mock（P1-2） | P1 | 空列表渲染 EmptyState，仅 `source==='mock'` 用 Mock | 空工作区夹具：0 假卡片 + 空态/重试 |
| 4 | 大仓树超时（P1-3） | P1 | git status 独立 2s 预算 + 失败降级 NONE + 先枚举后注解/并行 | 10 万文件夹具：`getRepoTree` ≤10s 成功、gitStatus 可 NONE、无 E_TIMEOUT |
| 5 | UI 超时无余量（P1-4） | P1 | `TIMEOUTS = Host + 5s`（或 Host 下发） | 造 9.9s 慢响应：UI 收到 Host 真实 code 而非前端 E_TIMEOUT |
| 6 | 超限/坏包 id:null 静默 + 字符/字节口径（P1-5） | P1 | 字节计长、尽量回显 id、前端兜底 reject、序列化前业务上限 | 70KB 中文请求返回可配对 `E_INVALID_REQUEST`；2MB+ 响应返回 `E_RESPONSE_TOO_LARGE` |
| 7 | confirm 布尔可伪造（P1-6） | P1（安全） | 一次性 `confirmToken`（HMAC+过期+审计） | 直接 `confirm:true` 无 token 被拒；正常 UI 确认可打开 |
| 8 | 提交列表跨组件污染（P1-7） | P1 | `loadCommits` 加 epoch+componentId+repoType 校验 | 快速切换组件后提交列表属于新组件 |
| 9 | reparse 防护不一致（P1-9） | P1（安全硬化） | 读/开统一 `includeTarget:true` + 最终路径解析复核 | 仓根被替换为 junction 后 `getRepoTree{path:""}` 返回 `E_REPARSE_POINT`；目录 junction open/reveal 被拒 |
| 10 | AC-UI-19 批量确认条件（P1-10） | P1 | SYNC/CREATE/DELETE/RESTORE 超阈值均确认 | 11 组件 SYNC → confirm-dialog 出现 |

> 另：P1-8（2MB/大组织快照）在“有 400+ 组件组织”上线前必须处理（放宽/分页 + UI 禁止静默 Mock）；当前夹具规模不触发，故列 P1 但排在 Top10 之后。

---

## 9. 修复顺序建议（先修再继续）

1. **第一天（阻断项）**：P0-1 + P1-1 + P1-2。这三项决定“用户看到的是不是真相、文件夹视图能不能用”，也是 AC-UI-14/33 的前提。
2. **第二天（预算与健壮性）**：P1-3 + P1-4 + P1-7 + P1-10；同时把 `ui/scripts/cdp-selfcheck.mjs` 接成真实 Host 模式并补 AC-14/19 断言。
3. **第三天（安全硬化）**：P1-5 + P1-6 + P1-9 + P2-11（DevTools/CSP/导航白名单/字节口径）。
4. **M1 出口前**：P1-8 大组织夹具验证、P2-8 矩阵单一事实源、`tests/contracts` golden、真实 Gitea 回归（§7.3）。
5. **复评门槛**：`dotnet test` 全绿 + 真实 Host CDP selfcheck 全绿 + 本文 §4 攻击矩阵回归用例入 CI。

---

## 10. 附录 A：本次探针关键原始证据（摘要）

```text
环境: app.local / WebView2 157.0.4341.46 / source=local+gitea
      workspace=D:\Workspace\git_1\artifacts\real-test\workspace
      components=4  repos=14  getSnapshot JSON = 8631 chars (≈2.16KB/component)
getHostInfo: {appVersion,webView2Version,features:{fileTree,workspaceTree,openPath,repoActions:false,ci:false,componentDetail},limits:{maxDirectoryEntries:2000,maxDepth:2,maxRequestBodyBytes:65536,maxResponseBytes:2097152,maxCommitsPerPage:200}}
[攻击矩阵] 31/31 拒绝: E_PATH_INVALID / E_PATH_OUTSIDE_ROOT / E_GIT_INTERNAL（逐条见 §4.2）
[openPath] 全部拒绝（24 组），无进程启动；`repo-scope+workspace-rel-path` → E_PATH_INVALID（fail-closed）
[getRepoTree path:'no-such-dir'] → {"ok":true,"data":{"entries":[],"error":"E_IO_ERROR",...}}   ← 前端丢弃 error
[2000 字符 x 64KB] 发送 70000 字符 getHostInfo → 6s 内无配对应答（Host 回 id:null 被前端丢弃）
[flood] 60×getHostInfo 并发 = 23ms 全成功；6×getRepoTree 并发 = 5ms（缓存命中）
[folder-view] expand COMPONENT → 8 nodes；expand REPO → 仍 8 nodes；getWorkspaceTree 请求数 = 1（只有 path:""）
[doubleclick repo-tree file] 拦截到 openPath {componentId:'RTCT_Demo',repoType:'IMPL',path:'component.txt',mode:'open',confirm:false}  ← 仓相对，正确
[context-menu reveal on remote-only component] 拦截到 openPath {path:'RTCT_RemoteOnly_Design',mode:'reveal'}  ← 期望路径不存在（P2-1）
[dotnet test] Core 71 passed / Host 145 passed / 0 failed / 0 skipped
```

**探针脚本**（可复跑，零依赖 Node ≥22，仅读 DOM/直发桥消息；openPath 均被拦截不转发）：
`%TEMP%\probe-v3.mjs`（攻击矩阵/参数/洪泛）、`probe-v3b.mjs`（设备名/别名/openPath 拦截）、`probe-v3c.mjs`（右键/资源管理器参数）、`probe4b.mjs`（error 字段）。
如评审环境重建，可参照 `ui/scripts/cdp-selfcheck.mjs` 的 CDP 连接方式复用。

## 11. 附录 B：本次未覆盖/无法验证项（避免误读结论）

1. **未实际执行危险的 openPath**（不启动 .exe/.bat/.ps1/.lnk），危险扩展名确认链以“拦截 postMessage + 代码路径”验证；真实启动行为需 E2E dry-run 通道。
2. **未创建 junction/符号链接/超长路径夹具**（会修改工作区夹具）；reparse 结论为代码路径 + 既有单测（且其 junction 用例在 mklink 失败时 silent pass）。
3. **未造 2000/5000/4000 组件、10 万提交、百万文件目录**：truncation/虚拟滚动/2MB 上限/性能预算为代码+量化外推，需按 §8 验收项补夹具实测。
4. **未在本机触发 CDP/DevTools 攻击**（仅作为 Release 硬化项）：`WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=9222` 运行态下，任何本地进程可驱动页面调用桥（含 `openPath confirm:true`），生产构建必须关闭/门控。
5. **未做长期运行/内存压测**（缓存 512 页 × 2000 节点最坏情形见 §5）。

---

**总体判定：安全主链路合格（P0 安全=0），但功能 P0=1、P1=10，暂不具备 AC 签收条件；建议按 §8 §9 顺序修复后复评。**
