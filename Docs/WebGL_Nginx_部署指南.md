# ChenZhenDemo WebGL Nginx 部署操作指南

## 一、构建输出

### 1.1 Unity WebGL 构建步骤

```
File → Build Settings → 选择 WebGL → Build → 选择输出目录
```

构建完成后，输出目录包含：

```
Build/
├── Build/
│   ├── ChenZhenDemo.data.gz      # 游戏数据（配置、资源）
│   ├── ChenZhenDemo.framework.js.gz  # Unity WebGL框架
│   ├── ChenZhenDemo.loader.js     # 加载器脚本
│   └── ChenZhenDemo.wasm.gz       # WebAssembly编译产物
├── TemplateData/
│   ├── UnityProgress.js           # 加载进度条脚本
│   ├── fullscreen-button.png      # 全屏按钮图标
│   └── style.css                  # 页面样式
└── index.html                     # 入口HTML
```

### 1.2 目录结构要求

部署时必须保持完整目录结构，不要拆分文件。

---

## 二、Nginx 部署

### 2.1 安装 Nginx

```bash
# Ubuntu/Debian
sudo apt update
sudo apt install nginx -y

# CentOS/RHEL
sudo yum install nginx -y

# macOS
brew install nginx
```

### 2.2 复制构建产物

```bash
# 创建部署目录
sudo mkdir -p /var/www/ChenZhenDemo

# 复制构建输出（假设构建产物在 ~/BuildOutput/）
sudo cp -r ~/BuildOutput/* /var/www/ChenZhenDemo/

# 设置权限
sudo chown -R www-data:www-data /var/www/ChenZhenDemo
sudo chmod -R 755 /var/www/ChenZhenDemo
```

### 2.3 Nginx 配置

创建配置文件：

```bash
sudo nano /etc/nginx/sites-available/ChenZhenDemo
```

写入以下内容：

```nginx
server {
    listen 80;
    server_name your-domain.com;  # 替换为你的域名或IP

    root /var/www/ChenZhenDemo;
    index index.html;

    # 主入口
    location / {
        try_files $uri $uri/ /index.html;
    }

    # gzip预压缩文件直接返回（Unity构建已生成.gz文件）
    location ~ \.gz$ {
        gzip off;
        add_header Content-Encoding gzip;
        add_header Cache-Control "public, max-age=31536000, immutable";
    }

    # 静态资源缓存
    location ~* \.(js|wasm|data|png|css)$ {
        expires 30d;
        add_header Cache-Control "public, max-age=2592000";
    }

    # MIME类型映射（关键！）
    types {
        application/wasm     wasm;
        application/gzip     gz;
        application/javascript js;
        application/json     json;
        text/html            html;
        text/css             css;
        image/png            png;
    }

    # 启用gzip压缩（对非.gz文件）
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml;
    gzip_min_length 256;

    # CORS头（如需跨域访问）
    add_header Cross-Origin-Opener-Policy same-origin;
    add_header Cross-Origin-Embedder-Policy require-corp;

    # 禁止缓存index.html（更新时需要）
    location = /index.html {
        expires -1;
        add_header Cache-Control "no-cache, no-store, must-revalidate";
    }
}
```

### 2.4 启用站点

```bash
# 创建软链接
sudo ln -s /etc/nginx/sites-available/ChenZhenDemo /etc/nginx/sites-enabled/

# 删除默认站点（可选）
sudo rm /etc/nginx/sites-enabled/default

# 测试配置
sudo nginx -t

# 重启Nginx
sudo systemctl restart nginx
```

### 2.5 验证部署

浏览器访问：

```
http://your-domain.com
http://your-server-ip
http://localhost:8080  （如果改了端口）
```

---

## 三、HTTPS 部署（可选但推荐）

### 3.1 使用 Let's Encrypt 免费证书

```bash
# 安装certbot
sudo apt install certbot python3-certbot-nginx -y

# 申请证书并自动配置Nginx
sudo certbot --nginx -d your-domain.com

# 自动续期（已自动配置cron）
sudo certbot renew --dry-run
```

### 3.2 Nginx HTTPS 完整配置

```nginx
server {
    listen 80;
    server_name your-domain.com;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name your-domain.com;

    ssl_certificate /etc/letsencrypt/live/your-domain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/your-domain.com/privkey.pem;

    root /var/www/ChenZhenDemo;
    index index.html;

    # ... 其余配置与2.3节相同 ...
}
```

---

## 四、常见问题排查

### 4.1 页面空白/无法加载

**原因**：MIME类型未配置，浏览器拒绝执行.wasm文件

**解决**：确认Nginx配置中有：

```nginx
types {
    application/wasm wasm;
    application/gzip gz;
}
```

### 4.2 控制台报 SharedArrayBuffer 错误

**原因**：缺少安全头

**解决**：Nginx配置中添加：

```nginx
add_header Cross-Origin-Opener-Policy same-origin;
add_header Cross-Origin-Embedder-Policy require-corp;
```

> 注意：这两个头必须同时设置，且必须在HTTPS下生效

### 4.3 资源404错误

**原因**：路径不对或目录权限问题

**解决**：

```bash
# 检查文件是否存在
ls -la /var/www/ChenZhenDemo/

# 检查权限
sudo chown -R www-data:www-data /var/www/ChenZhenDemo
sudo chmod -R 755 /var/www/ChenZhenDemo

# 检查Nginx错误日志
sudo tail -f /var/log/nginx/error.log
```

### 4.4 WebAssembly加载失败

**原因**：浏览器不支持或.gz文件损坏

**解决**：

```bash
# 验证.gz文件完整性
gunzip -t ChenZhenDemo.data.gz
gunzip -t ChenZhenDemo.framework.js.gz
gunzip -t ChenZhenDemo.wasm.gz
```

### 4.5 存档数据丢失

**原因**：WebGL的PlayerPrefs存储在浏览器IndexedDB中，清除浏览器数据会丢失存档

**解决**：提示用户不要清除站点数据，或实现服务器端存档（需后端API）

### 4.6 内存不足

**原因**：WebGL内存限制

**解决**：在Unity Player Settings中：

```
Player Settings → Publishing Settings → Memory Size → 设置为256或512
```

---

## 五、性能优化

### 5.1 启用Brotli压缩（推荐）

```bash
sudo apt install libnginx-mod-http-brotli -y
```

Nginx配置：

```nginx
brotli on;
brotli_types application/javascript application/wasm text/plain text/css;
brotli_comp_level 6;
```

> Brotli比gzip压缩率高15-20%，显著减小包体加载时间

### 5.2 CDN加速

将构建产物上传至CDN：

```bash
# 以阿里云OSS为例
ossutil cp -r /var/www/ChenZhenDemo/ oss://your-bucket/game/ --exclude "index.html"
```

Nginx中将静态资源指向CDN：

```javascript
// 修改buildUrl（在Unity生成的loader.js中）
var buildUrl = "https://your-cdn.com/game/Build";
```

### 5.3 浏览器缓存策略

```nginx
# 长期缓存（带版本号的文件）
location ~* \.(data|framework|wasm)\.[a-f0-9]+\.(gz|js)$ {
    expires 1y;
    add_header Cache-Control "public, max-age=31536000, immutable";
}

# 短期缓存
location ~* \.(png|css)$ {
    expires 7d;
    add_header Cache-Control "public, max-age=604800";
}
```

---

## 六、Docker 部署（可选）

### 6.1 Dockerfile

```dockerfile
FROM nginx:alpine

# 复制构建产物
COPY Build/ /usr/share/nginx/html/

# 复制Nginx配置
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
```

### 6.2 nginx.conf

```nginx
server {
    listen 80;
    root /usr/share/nginx/html;
    index index.html;

    location / {
        try_files $uri $uri/ /index.html;
    }

    location ~ \.gz$ {
        gzip off;
        add_header Content-Encoding gzip;
    }

    types {
        application/wasm wasm;
        application/gzip gz;
    }

    add_header Cross-Origin-Opener-Policy same-origin;
    add_header Cross-Origin-Embedder-Policy require-corp;
}
```

### 6.3 构建和运行

```bash
docker build -t chenzhen-demo .
docker run -d -p 8080:80 chenzhen-demo
```

访问：`http://localhost:8080`

---

## 七、自动化部署脚本

### deploy.sh

```bash
#!/bin/bash

# 配置
DEPLOY_DIR="/var/www/ChenZhenDemo"
BUILD_DIR="./Build"
NGINX_CONF="/etc/nginx/sites-available/ChenZhenDemo"

echo "=== ChenZhenDemo WebGL 部署脚本 ==="

# 1. 检查构建产物
if [ ! -d "$BUILD_DIR" ]; then
    echo "错误：构建目录不存在，请先执行Unity WebGL构建"
    exit 1
fi

# 2. 创建部署目录
echo "创建部署目录..."
sudo mkdir -p $DEPLOY_DIR

# 3. 复制文件
echo "复制构建产物..."
sudo cp -r $BUILD_DIR/* $DEPLOY_DIR/

# 4. 设置权限
echo "设置文件权限..."
sudo chown -R www-data:www-data $DEPLOY_DIR
sudo chmod -R 755 $DEPLOY_DIR

# 5. 配置Nginx
echo "配置Nginx..."
sudo cp nginx.conf $NGINX_CONF
sudo ln -sf /etc/nginx/sites-available/ChenZhenDemo /etc/nginx/sites-enabled/
sudo rm -f /etc/nginx/sites-enabled/default

# 6. 测试并重启
echo "测试Nginx配置..."
sudo nginx -t
if [ $? -eq 0 ]; then
    sudo systemctl restart nginx
    echo "=== 部署完成！==="
    echo "访问地址: http://$(hostname -I | awk '{print $1}')"
else
    echo "错误：Nginx配置测试失败"
    exit 1
fi
```

使用方式：

```bash
chmod +x deploy.sh
sudo ./deploy.sh
```

---

## 八、验收检查清单

| 检查项 | 命令/操作 | 预期结果 |
|--------|-----------|----------|
| Nginx运行状态 | `systemctl status nginx` | active (running) |
| 端口监听 | `ss -tlnp \| grep :80` | nginx进程监听80端口 |
| 页面可访问 | 浏览器访问http://IP | 游戏加载页面正常显示 |
| 游戏可运行 | 点击Play按钮 | 游戏正常启动，无控制台报错 |
| 存档功能 | 游戏中保存→刷新页面→加载 | 存档数据完整还原 |
| 音效播放 | 游戏中触发战斗 | 音效正常播放 |
| 面板快捷键 | 按I/O/T/K/J/L | 各面板正常打开/关闭 |
| WebGL控制台 | F12打开开发者工具 | 无红色错误，仅有绿色日志 |

---

**部署完成！** 🎮
