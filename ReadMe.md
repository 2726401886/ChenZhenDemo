# ChenZhenDemo

第三人称动作RPG Demo | Unity WebGL

## 在线体验

🔗 [点击游玩](https://2726401886.github.io/ChenZhenDemo/)

## 功能特性

- 基础战斗系统 + 5项技能（普攻/旋风斩Q/冲刺E/重击R/闪避Space）
- 6种敌人AI（近战/自爆/远程法师/精英/远程/召唤Boss）
- 对象池性能优化
- 背包系统（20格） + 装备系统（4部位）
- 装备强化系统（最高+3，失败不掉级）
- 商店NPC买卖系统
- 金币经济系统
- 成就系统（6项成就）
- 主线任务剧情系统（5段主线，NPC对话）
- SaveGame存档读写
- 全套UI面板（I/O/T/K/J/L）
- WebGL兼容

## 操作按键

| 按键 | 功能 |
|------|------|
| WASD | 移动 |
| 鼠标 | 视角旋转 |
| 左键 | 普通攻击 |
| Q | 旋风斩 |
| E | 冲刺突进 |
| R | 重击 |
| Space | 闪避 |
| I | 背包 |
| O | 装备 |
| T | 商店 |
| K | 强化 |
| J | 成就 |
| L | 任务 |
| F | NPC对话 |
| Esc | 暂停 |

## 本地开发

```bash
# 使用Unity Hub打开项目
Unity Hub → Open → 选择 ChenZhenDemo 目录

# 运行测试
点击 Play 按钮
```

## WebGL打包

```bash
# Unity编辑器内打包
菜单栏 → Build → Build WebGL (GitHub Pages)

# 产物输出到 docs/ 目录
```

## GitHub Pages 部署

1. 推送代码到 `main` 分支
2. 仓库 Settings → Pages → Source: `gh-pages`
3. 等待 GitHub Actions 自动打包部署
4. 访问 https://2726401886.github.io/ChenZhenDemo/

## 项目结构

```
ChenZhenDemo/
├── Assets/
│   ├── Scripts/
│   │   ├── Core/        # 核心系统（EventBus/SaveManager/配置表）
│   │   ├── Player/      # 玩家控制/背包/装备/强化/任务/成就
│   │   ├── Enemy/       # 敌人AI（6种）
│   │   ├── Combat/      # 战斗系统/物品/装备数据
│   │   ├── UI/          # 全套UI面板
│   │   ├── NPC/         # NPC交互/对话
│   │   └── Camera/      # 摄像机
│   └── Editor/          # 打包脚本
├── .github/workflows/   # CI自动打包
├── Docs/                # 文档
└── Tools/               # 工具脚本
```

## 技术栈

- Unity 2022.3 LTS
- C# / WebGL / Wasm
- EventBus 事件驱动架构
- PlayerPrefs 存档（WebGL兼容）

## 许可证

MIT
