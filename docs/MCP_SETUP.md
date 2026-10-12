# Unity MCP 接入指南（队友版）

本仓库已内置 MCP for Unity。队友拉取代码后按本文配置即可让 AI 客户端直接操控你的 Unity 编辑器。

---

## 前置条件

| 依赖 | 版本要求 | 检查命令 |
|---|---|---|
| Unity | 2022.3.62f3c1（与 `ProjectSettings/ProjectVersion.txt` 一致） | — |
| uv / uvx | 任意近期版本 | `uv --version` |

**安装 uv（Windows）**

```powershell
powershell -c "irm https://astral.sh/uv/install.ps1 | iex"
```

安装后重启终端，确认 `uvx` 已在 PATH 中：

```powershell
uvx --version
```

> Linux / macOS 用`curl -LsSf https://astral.sh/uv/install.sh | sh`

---

## 第一步：拉取代码

```bash
git clone <仓库地址>
cd 2026GamejamProject
```

用 Unity 2022.3.62f3c1 打开项目。首次打开时 Unity 会自动从 `Packages/manifest.json` 解析依赖，
`com.coplaydev.unity-mcp`（本地包，位于 `Packages/com.coplaydev.unity-mcp/`）会被自动加载。
等待编译完成，确认 Console 无报错。

---

## 第二步：配置 MCP 客户端

本仓库提供了**项目级**配置 `.workbuddy/mcp.json`，内容对所有队友一致：

```json
{
  "mcpServers": {
    "UnityMCP": {
      "command": "uvx",
      "args": ["--from", "mcpforunityserver==10.3.0", "mcp-for-unity"],
      "description": "MCP for Unity - 通过 stdio 操控 Unity 编辑器"
    }
  }
}
```

### WorkBuddy（推荐）

1. 重启 WorkBuddy，或在「插件 → 右上角 MCP 服务器 → 配置 MCP」中打开编辑器
2. 项目级配置会自动加载 `.workbuddy/mcp.json`
3. 在 MCP 服务器列表中确认 `UnityMCP` 状态为🟢 绿色

若显示红色，依次排查：
- `uvx` 是否在 PATH（在终端跑一次 `uvx --version`）
- 版本号是否与项目包一致（见下方"版本同步"）

### 其他客户端

Unity 内置了多种客户端的自动配置器，可直接写入对应配置文件，无需手写 JSON：

**Window → MCP for Unity → Configurator**，选择你的客户端（Claude Code / Cursor / CodeBuddy CLI 等），点击 Configure。

---

## 第三步：连通性验证

在 AI 客户端里依次测试：

1. 「读取 Unity 控制台」→ 应返回日志列表（无报错即正常）
2. 「获取当前场景信息」→ 应返回当前打开的场景路径
3. 「在场景里创建一个立方体」→ 确认 Unity 编辑器中出现该物体

Unity 侧的运行状态可在 **Window → MCP for Unity** 面板查看，
面板会显示连接状态与工具组（Tool Groups）开关。

---

## 常见问题

**Q：MCP 面板显示未连接**

A：Unity 侧需要 MCP 服务在运行。确认 Unity 编辑器已打开，且 MCP for Unity 面板中的连接已启动。

**Q：端口冲突**

A：MCP 默认使用 6400 端口，被占用时会自动向后寻找可用端口。
如需固定端口，在 MCP for Unity 面板的高级设置里修改。

**Q：AI 客户端看不到某个工具**

A：工具按组（Tool Groups）控制。默认只启用 `core` 组，
`docs`（API 文档查询）、`testing`（测试运行）、`profiling`、`animation`、`ui`、`vfx`
等组默认关闭。在 MCP for Unity 面板中启用对应组后，再在客户端执行 `sync`。

> 注意：工具组开关保存在**本地编辑器**（EditorPrefs），不进版本库。
> 每个队友需要在自己机器上启用一次。

**Q：`uvx` 找不到 / 版本不匹配**

A：MCP server 版本必须与项目内的 Unity 包版本一致。当前项目为 `10.3.0`。
若 Unity 包升级了，需同步更新 `mcp.json` 里的版本号。

---

## 版本同步

这两个位置的版本号必须一致：

| 位置 | 文件 | 当前值 |
|---|---|---|
| Unity 包 | `Packages/com.coplaydev.unity-mcp/package.json` → `version` | `10.3.0` |
| MCP 服务端 | `.workbuddy/mcp.json` → `args` 里的 `mcpforunityserver==` | `10.3.0` |

升级 Unity 包后，务必同步更新 `.workbuddy/mcp.json`，否则队友会拉到版本不匹配的 MCP server。

---

## 安全提醒

- `.workbuddy/mcp.json` 里**不要**填写任何密钥、Token、Webhook 地址
- MCP server 以 stdio 模式在本机运行，能执行 Unity 编辑器命令（含 C# 代码执行）。只给可信的人开
- `.workbuddy/memory/` 是本地记忆目录，已在 `.gitignore` 中排除，不要提交