# SymSmartQueue for Jellyfin

SymSmartQueue is an advanced, AI-powered music queue generator for Jellyfin. It uses the Essentia machine learning library to natively analyze your audio files for BPM, Danceability, and heuristic mood vectors (Party, Relaxed, Sad, etc.) to generate dynamic, seamless smart queues.

## Features
* **Native Acoustic Analysis:** Extracts deep telemetry directly from your `.mp3` and `.flac` files without relying on external APIs.
* **Heuristic Moods:** Generates queues based on 12 distinct moods and ID3 language tags.
* **Auto-Scaling Tolerance:** Learns from your skip/complete behavior to tighten or loosen the sonic boundaries of your recommendations over time.

## Installation (Docker Users)
This plugin utilizes a self-compiling bootstrapper. It will automatically build the required C++ Machine Learning dependencies in the background upon installation.

1. Go to your Jellyfin **Dashboard** -> **Plugins** -> **Repositories**.
2. Add a new repository with the following URL:
   `https://raw.githubusercontent.com/thirdu9/SYM-Smart-Queue/refs/heads/main/manifest.json`
3. Go to the **Catalog** tab, find **SymSmartQueue**, and click **Install**.
4. Restart your Jellyfin container. 
5. *(Note: The initial background compilation may take 10-15 minutes. Check your Jellyfin logs to monitor the build progress).*