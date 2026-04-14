#!/bin/bash
# SymSmartQueue Background Installer (Pre-compiled)
set -e

SYM_DIR="$1"
echo "Starting fast installation targeting: $SYM_DIR"

# 1. Download the complete payload (Binary + Models)
echo "Downloading ML engine and models..."
curl -L -o "$SYM_DIR/sym_payload.zip" "https://github.com/thirdu9/SYM-Smart-Queue/releases/download/Bin-files/Sym_Queue_Bin.zip"

# 2. Extract and clean up
echo "Extracting payload..."
unzip -o "$SYM_DIR/Sym_Queue_Bin.zip" -d "$SYM_DIR/"
rm "$SYM_DIR/Sym_Queue_Bin.zip"

# 3. Set executable permissions for the binary
echo "Setting permissions..."
chmod +x "$SYM_DIR/essentia_streaming_extractor_music"

echo "Installation complete! Engine is ready."