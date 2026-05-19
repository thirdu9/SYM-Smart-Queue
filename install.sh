################################################################
#!/bin/bash
# SymSmartQueue Background Installer (Pre-compiled & Self-Contained)
set -e

SYM_DIR="$1"        # Jellyfin plugin config dir, e.g. /config/data/plugins/configurations/SymSmartQueue
PLUGIN_DIR="$2"     # Jellyfin plugin DLL dir,    e.g. /config/data/plugins/SYM-QUEUE-ENGINE

echo "================================================================"
echo " SYM Smart Queue Installer"
echo " Config Dir : $SYM_DIR"
echo " Plugin Dir : $PLUGIN_DIR"
echo "================================================================"

# 1. Install dependencies ONLY if we have root permissions (Just in case)
if [ "$(id -u)" -eq 0 ]; then
    echo "[1/4] Root detected. Ensuring curl is available..."
    apt-get update > /dev/null 2>&1 || true
    apt-get install -y curl > /dev/null 2>&1 || true
else
    echo "[1/4] Non-root user. Relying on pre-installed tools..."
fi

RELEASE_BASE="https://github.com/thirdu9/SYM-Smart-Queue/releases/download"

# 2. Download and extract the Essentia ML engine + profile
echo "[2/4] Downloading Essentia engine and analysis profile..."
curl -L -f -o "$SYM_DIR/Sym_Queue_Bin.tar.gz" "$RELEASE_BASE/Bin-files/Sym_Queue_Bin.tar.gz"

echo "      Extracting engine binaries..."
tar -xzf "$SYM_DIR/Sym_Queue_Bin.tar.gz" -C "$SYM_DIR/"
rm "$SYM_DIR/Sym_Queue_Bin.tar.gz"

if [ -d "$SYM_DIR/Sym_Queue_Bin" ]; then
    echo "      Flattening nested directory..."
    mv "$SYM_DIR/Sym_Queue_Bin/"* "$SYM_DIR/"
    rm -rf "$SYM_DIR/Sym_Queue_Bin"
fi

chmod +x "$SYM_DIR/essentia_streaming_extractor_music"
echo "      Essentia engine ready."

# 3. Download and extract the Whisper ONNX language identification models
WHISPER_MODEL_DIR="$SYM_DIR/models/whisper"
mkdir -p "$WHISPER_MODEL_DIR"

if [ ! -f "$WHISPER_MODEL_DIR/encoder.onnx" ] || [ ! -f "$WHISPER_MODEL_DIR/decoder.onnx" ]; then
    echo "[3/4] Downloading Whisper ONNX language identification models..."
    curl -L -f -o "/tmp/whisper-models.tar.gz" "$RELEASE_BASE/whisper-runtime-files/whisper-models.tar.gz"

    echo "      Extracting models to $WHISPER_MODEL_DIR..."
    tar -xzf "/tmp/whisper-models.tar.gz" -C "$WHISPER_MODEL_DIR/"
    rm "/tmp/whisper-models.tar.gz"
    echo "      Whisper models ready."
else
    echo "[3/4] Whisper models already present. Skipping."
fi

# 4. Download sherpa-onnx native runtime libraries into the plugin DLL directory
if [ -n "$PLUGIN_DIR" ]; then
    if [ ! -f "$PLUGIN_DIR/libsherpa-onnx-c-api.so" ] || [ ! -f "$PLUGIN_DIR/libonnxruntime.so" ]; then
        echo "[4/4] Downloading sherpa-onnx native runtime libraries..."
        curl -L -f -o "/tmp/runtimes-linux.tar.gz" "$RELEASE_BASE/whisper-runtime-files/runtimes-linux.tar.gz"

        echo "      Extracting native libs to $PLUGIN_DIR..."
        tar -xzf "/tmp/runtimes-linux.tar.gz" -C "$PLUGIN_DIR/"
        rm "/tmp/runtimes-linux.tar.gz"
        echo "      Native libraries ready."
    else
        echo "[4/4] Native runtime libraries already present. Skipping."
    fi
else
    echo "[4/4] PLUGIN_DIR not specified. Skipping native runtime download."
    echo "      Pass the plugin DLL directory as the second argument to install native AI libs."
fi

echo ""
echo "================================================================"
echo " Installation complete! SYM Engine is ready."
echo "================================================================"
