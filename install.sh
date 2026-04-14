################################################################
#!/bin/bash
# SymSmartQueue Background Installer (Pre-compiled & Self-Contained)
set -e

SYM_DIR="$1"
echo "Starting fast installation targeting: $SYM_DIR"

# 1. Install dependencies ONLY if we have root permissions (Just in case)
if [ "$(id -u)" -eq 0 ]; then
    echo "Root privileges detected. Ensuring curl is installed..."
    apt-get update > /dev/null || true
    apt-get install -y curl > /dev/null || true
else
    echo "Non-root user detected. Relying on native tools..."
fi

# 2. Download the complete payload (Now pointing to the .tar.gz)
echo "Downloading ML engine and models..."
PAYLOAD_URL="https://github.com/thirdu9/SYM-Smart-Queue/releases/download/Bin-files/Sym_Queue_Bin.zip"
curl -L -f -o "$SYM_DIR/Sym_Queue_Bin.tar.gz" "$PAYLOAD_URL"

# 3. Extract and clean up using native tar
echo "Extracting Binaries and Dependencies..."
tar -xzf "$SYM_DIR/Sym_Queue_Bin.tar.gz" -C "$SYM_DIR/"
rm "$SYM_DIR/Sym_Queue_Bin.tar.gz"

if [ -d "$SYM_DIR/Sym_Queue_Bin" ]; then
    echo "Flattening nested directory structure..."
    mv "$SYM_DIR/Sym_Queue_Bin/"* "$SYM_DIR/"
    rm -rf "$SYM_DIR/Sym_Queue_Bin"
fi

# 4. Set executable permissions
echo "Setting permissions..."
chmod +x "$SYM_DIR/essentia_streaming_extractor_music"

echo "Installation complete! Engine is ready."