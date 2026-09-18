#!/bin/bash
# ChenZhenDemo WebGL 产物校验脚本

echo "=== ChenZhenDemo WebGL 打包产物校验 ==="
echo ""

BUILD_DIR="docs"
ERRORS=0

# 1. 检查目录结构
echo "[1/5] 检查目录结构..."
for item in "index.html" ".nojekyll" "Build" "TemplateData"; do
    if [ -e "$BUILD_DIR/$item" ]; then
        echo "  ✅ $item 存在"
    else
        echo "  ❌ $item 缺失"
        ERRORS=$((ERRORS + 1))
    fi
done

# 2. 检查Build目录文件
echo ""
echo "[2/5] 检查Build目录文件..."
if [ -d "$BUILD_DIR/Build" ]; then
    WASM_COUNT=$(find "$BUILD_DIR/Build" -name "*.wasm" | wc -l)
    DATA_COUNT=$(find "$BUILD_DIR/Build" -name "*.data" | wc -l)
    JS_COUNT=$(find "$BUILD_DIR/Build" -name "*.js" | wc -l)
    
    echo "  WASM文件: $WASM_COUNT"
    echo "  Data文件: $DATA_COUNT"
    echo "  JS文件: $JS_COUNT"
    
    if [ "$WASM_COUNT" -eq 0 ]; then
        echo "  ❌ 缺少.wasm文件"
        ERRORS=$((ERRORS + 1))
    fi
    if [ "$DATA_COUNT" -eq 0 ]; then
        echo "  ❌ 缺少.data文件"
        ERRORS=$((ERRORS + 1))
    fi
else
    echo "  ❌ Build目录不存在"
    ERRORS=$((ERRORS + 1))
fi

# 3. 检查文件大小
echo ""
echo "[3/5] 检查文件大小..."
TOTAL_SIZE=$(du -sh "$BUILD_DIR" 2>/dev/null | cut -f1)
echo "  总大小: $TOTAL_SIZE"

if [ -d "$BUILD_DIR/Build" ]; then
    for f in "$BUILD_DIR/Build"/*; do
        SIZE=$(du -sh "$f" 2>/dev/null | cut -f1)
        echo "  $(basename $f): $SIZE"
    done
fi

# 4. 检查nojekyll
echo ""
echo "[4/5] 检查.nojekyll..."
if [ -f "$BUILD_DIR/.nojekyll" ]; then
    echo "  ✅ .nojekyll 存在"
else
    echo "  ❌ .nojekyll 缺失（GitHub Pages需要）"
    ERRORS=$((ERRORS + 1))
fi

# 5. 检查index.html内容
echo ""
echo "[5/5] 检查index.html..."
if [ -f "$BUILD_DIR/index.html" ]; then
    if grep -q "ChenZhenDemo" "$BUILD_DIR/index.html" 2>/dev/null; then
        echo "  ✅ index.html 包含项目引用"
    else
        echo "  ⚠️ index.html 未找到项目引用"
    fi
else
    echo "  ❌ index.html 不存在"
    ERRORS=$((ERRORS + 1))
fi

# 汇总
echo ""
echo "================================"
if [ $ERRORS -eq 0 ]; then
    echo "✅ 校验通过！产物完整"
else
    echo "❌ 校验失败，发现 $ERRORS 个问题"
fi
echo "================================"
