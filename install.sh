#!/bin/bash
# SymSmartQueue Background Docker Compiler (with Gaia)
set -e

SYM_DIR="$1"
echo "Starting background compilation targeting: $SYM_DIR"

# 1. Install Build Dependencies
echo "Installing C++ build dependencies..."
apt-get update
apt-get install -y build-essential git python3 python3-dev pkg-config \
    libfftw3-dev libavcodec-dev libavformat-dev libavutil-dev \
    libswresample-dev libsamplerate0-dev libtag1-dev libyaml-dev \
    libqt4-dev qtbase5-dev curl unzip

# 2. Clone and Compile Gaia
echo "Cloning and compiling Gaia..."
cd /tmp
rm -rf gaia
git clone https://github.com/MTG/gaia.git
cd gaia
python3 waf configure --mode=release
python3 waf
python3 waf install
ldconfig # Crucial: Refreshes system libraries so Essentia can find libgaia2

# 3. Clone and Compile Essentia (Linked with Gaia)
echo "Cloning and compiling Essentia..."
cd /tmp
rm -rf essentia
git clone https://github.com/MTG/essentia.git
cd essentia
python3 -m venv build_env
./build_env/bin/python3 -m pip install setuptools
./build_env/bin/python3 waf configure --mode=release --with-examples --with-gaia
./build_env/bin/python3 waf

# 4. Move the compiled binary to the PERSISTENT volume
echo "Moving compiled binary to persistent volume..."
cp build/src/examples/essentia_streaming_extractor_music "$SYM_DIR/"
chmod +x "$SYM_DIR/essentia_streaming_extractor_music"

# 5. Download ML Models and YAML
echo "Downloading ML models..."
curl -L -o "$SYM_DIR/sym_models.zip" "https://github.com/thirdu9/SYM-Smart-Queue/releases/download/v1.0.0/svm_models.zip"

echo "Extracting models..."
unzip -o "$SYM_DIR/sym_models.zip" -d "$SYM_DIR/"
rm "$SYM_DIR/sym_models.zip"

# 6. Clean up
echo "Cleaning up build cache..."
rm -rf /tmp/gaia /tmp/essentia
apt-get clean

echo "Compilation complete!"