##########################################################
#!/bin/bash
# SymSmartQueue Background Installer (Pre-compiled)
set -e

SYM_DIR="$1"
echo "Starting fast installation targeting: $SYM_DIR"

# 1. Install extraction tools AND runtime C++ dependencies
echo "Ensuring runtime dependencies are installed..."
apt-get update > /dev/null
apt-get install -y curl unzip libfftw3-dev libavcodec-dev libavformat-dev \
    libavutil-dev libswresample-dev libsamplerate0-dev libtag1-dev \
    libyaml-dev qtbase5-dev libeigen3-dev > /dev/null

# 2. Download the complete payload
echo "Downloading ML engine and models..."
PAYLOAD_URL="https://github.com/thirdu9/SYM-Smart-Queue/releases/download/Bin-files/Sym_Queue_Bin.zip"
curl -L -f -o "$SYM_DIR/Sym_Queue_Bin.zip" "$PAYLOAD_URL"

# 3. Extract and clean up
echo "Extracting payload..."
unzip -o "$SYM_DIR/Sym_Queue_Bin.zip" -d "$SYM_DIR/"
rm "$SYM_DIR/Sym_Queue_Bin.zip"

if [ -d "$SYM_DIR/Sym_Queue_Bin" ]; then
    echo "Flattening nested directory structure..."
    mv "$SYM_DIR/Sym_Queue_Bin/"* "$SYM_DIR/"
    rm -rf "$SYM_DIR/Sym_Queue_Bin"
fi

# 4. Set executable permissions
echo "Setting permissions..."
chmod +x "$SYM_DIR/essentia_streaming_extractor_music"

echo "Installation complete! Engine is ready."