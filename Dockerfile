# APK archive inspection environment. Keep project tooling in this container.
FROM debian:trixie-slim

RUN apt-get update \
    && apt-get install --yes --no-install-recommends \
        ca-certificates \
        clamav \
        curl \
        file \
        git \
        libicu76 \
        libssl3t64 \
        libarchive-tools \
        mono-devel \
        mono-utils \
        openjdk-21-jdk-headless \
        python3 \
        python3-venv \
        apksigner \
        zipalign \
        aapt \
        libsmali-java \
        adb \
        unzip \
        zip \
    && rm -rf /var/lib/apt/lists/*

# Analysis only; no SDKs or dependencies are installed on the host.
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --version 8.0.32 --runtime dotnet --install-dir /opt/dotnet \
    && mkdir -p /opt/ilspy \
    && curl -fsSL https://api.nuget.org/v3-flatcontainer/ilspycmd/9.1.0.7988/ilspycmd.9.1.0.7988.nupkg -o /tmp/ilspycmd.nupkg \
    && unzip -q /tmp/ilspycmd.nupkg -d /opt/ilspy \
    && python3 -m venv /opt/unity-tools \
    && /opt/unity-tools/bin/pip install --no-cache-dir UnityPy==1.24.2 \
    && rm /tmp/dotnet-install.sh /tmp/ilspycmd.nupkg

# Compile only the document-picker bridge; these jars are never packaged.
RUN mkdir -p /opt/dg-save-tools \
    && curl -fsSL https://dl.google.com/android/maven2/com/android/tools/r8/8.3.37/r8-8.3.37.jar -o /opt/dg-save-tools/r8.jar \
    && curl -fsSL https://repo.maven.apache.org/maven2/com/google/android/android/4.1.1.4/android-4.1.1.4.jar -o /opt/dg-save-tools/android.jar \
    && curl -fsSL https://repo.maven.apache.org/maven2/org/json/json/20080701/json-20080701.jar -o /opt/dg-save-tools/json.jar

WORKDIR /workspace

CMD ["bash"]
