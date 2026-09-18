# WebGL 打包适配配置说明

## 项目概述
本项目为Unity第三人称动作Demo，已进行WebGL平台兼容适配，可直接打包为WebGL版本。

## Unity版本要求
- Unity 2022.3 LTS 或更高版本
- 需安装 WebGL Build Support 模块

## PlayerSettings 推荐配置

### 1. 基本设置（Edit > Project Settings > Player）

| 设置项 | 推荐值 | 说明 |
|--------|--------|------|
| Company Name | 自定义 | 公司名称 |
| Product Name | ChenZhenDemo | 产品名称 |
| Version | 1.0.0 | 版本号 |

### 2. WebGL特定设置（Player > WebGL Settings）

| 设置项 | 推荐值 | 说明 |
|--------|--------|------|
| Compression Format | Brotli | 压缩率最高，加载最快 |
| Template | Minimal | 最小模板，减小包体 |
| Debug Symbols | 关闭 | 发布时关闭调试符号 |
| Enable C++ Exception | 关闭 | 减小包体 |
| Name Files As Hashes | 开启 | 缓存友好 |
| Data Caching | 开启 | 缓存构建数据 |
| Linker Target | Wasm | 使用WebAssembly |

### 3. 其他设置（Player > Other Settings）

| 设置项 | 推荐值 | 说明 |
|--------|--------|------|
| Color Space | Linear | 推荐使用线性色彩空间 |
| Auto Graphics API | 关闭 | 手动选择OpenGL ES3 |
| Minimum OpenGL ES3 | 3.0 | WebGL2.0基于此 |
| Use Player Log | 关闭 | 发布时关闭日志 |
| Use Mac App Store Validation | 关闭 | 非Mac平台 |

### 4. 推荐内存配置

| 设置项 | 推荐值 | 说明 |
|--------|--------|------|
| Initial Memory Size | 256 MB | 初始内存 |
| Maximum Memory Size | 512 MB | 最大内存 |
| Use Wasm Memory | true | 使用WebAssembly内存 |
| Use Incremental GC | true | 增量垃圾回收 |

### 5. 禁用未使用的模块

在 **Project Settings > Player > Other Settings > Configuration** 中：

| 模块 | 建议 | 说明 |
|------|------|------|
| UnityWebAudio | 根据需要 | 如不使用WebAudio可禁用 |
| UnityWebRequest | 保持启用 | 网络请求需要 |
| UnityWebRequestTexture | 根据需要 | 如不使用WWW可禁用 |

## 打包步骤

### 步骤1：安装WebGL支持
1. 打开 Unity Hub
2. 选择项目使用的Unity版本
3. 点击 "Add Modules"
4. 勾选 "WebGL Build Support"
5. 等待安装完成

### 步骤2：配置PlayerSettings
1. 打开 Edit > Project Settings > Player
2. 选择 WebGL Tab
3. 按照上述推荐值配置所有设置

### 步骤3：构建打包
1. 选择 File > Build Settings
2. 选择 WebGL 平台
3. 点击 Switch Platform
4. 设置输出目录（如 `Build/WebGL`）
5. 点击 Build And Run

### 步骤4：验证打包
1. 等待构建完成（首次可能较慢）
2. 浏览器自动打开，验证游戏运行
3. 检查控制台是否有错误
4. 测试所有功能是否正常

## WebGL平台兼容处理

### 1. 单例模式兼容
- 文件：`Core/Singleton.cs`
- 处理：DontDestroyOnLoad边界保护、退出标志重置
- 说明：WebGL场景切换不触发OnApplicationQuit，需要手动重置

### 2. 事件系统兼容
- 文件：`Core/EventBus.cs`
- 处理：日志采样输出，避免控制台卡顿
- 参数：WebGLLogSampleRate = 0.1（每10个事件输出1个日志）

### 3. 时间缩放兼容
- 文件：`Core/WebGLPlatformHelper.cs`
- 处理：封装Time.timeScale，输出调试日志
- 说明：WebGL中时间缩放正常工作，但需要输出日志便于调试

### 4. 协程兼容
- 文件：`Core/WebGLPlatformHelper.cs`
- 处理：SecondsToFrames方法将秒数转换为帧数
- 说明：WebGL中WaitForSeconds性能较差，可使用帧计数替代

### 5. 日志输出优化
- 文件：`Core/EventBus.cs`, `Core/WebGLPlatformHelper.cs`
- 处理：WebGL平台日志采样输出
- 说明：避免控制台大量日志导致浏览器卡顿

## 已知限制

### 1. 性能限制
- WebGL为单线程，不支持多线程
- GPU功能可能受限（取决于浏览器和显卡）
- 内存受限于浏览器和系统配置

### 2. 输入限制
- 鼠标锁（Cursor.lockState）在WebGL中可能不可用
- 某些特殊键盘按键可能无法检测
- 移动端触摸输入需要额外处理

### 3. 音频限制
- WebGL需要用户交互后才能播放音频
- 建议在首次点击时初始化音频系统

### 4. 文件系统
- WebGL使用IndexedDB存储持久化数据
- 文件大小受限于浏览器配额

## 优化建议

### 1. 包体优化
- 使用Brotli压缩
- 启用Asset Bundle
- 纹理使用ASTC压缩
- 音频使用Vorbis压缩

### 2. 性能优化
- 使用对象池减少Instantiate/Destroy
- 启用增量GC
- 减少每帧的计算量
- 使用LOD（Level of Detail）

### 3. 内存优化
- 及时卸载未使用的资源
- 使用Resources.UnloadUnusedAssets()
- 避免内存泄漏

## 测试清单

### 基本功能测试
- [ ] 游戏正常启动
- [ ] 玩家控制正常
- [ ] 敌人AI正常
- [ ] 战斗系统正常
- [ ] UI显示正常

### WebGL特定测试
- [ ] 浏览器标签页切换后恢复
- [ ] 窗口大小调整后UI适配
- [ ] 全屏模式切换
- [ ] 音频播放正常（需要用户交互）
- [ ] 内存使用稳定

### 性能测试
- [ ] 帧率稳定（目标30FPS以上）
- [ ] 内存无明显泄漏
- [ ] 加载时间可接受

## 常见问题

### Q: 游戏无法启动？
A: 检查浏览器控制台是否有错误，确保WebGL支持已启用。

### Q: 音频没有声音？
A: WebGL需要用户交互后才能播放音频，确保在点击后初始化音频。

### Q: 帧率很低？
A: 检查是否开启了增量GC，减少每帧计算量，使用对象池。

### Q: 内存溢出？
A: 减少纹理大小，使用对象池，及时卸载未使用资源。

## 相关文档
- Unity WebGL官方文档：https://docs.unity3d.com/Manual/webgl.html
- WebGL性能优化：https://docs.unity3d.com/Manual/webgl-performance.html
