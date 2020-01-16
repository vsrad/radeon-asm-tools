FROM ubuntu:16.04

# dev tools, application dependencies and ROCm packages
RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends curl && \
  curl -sL http://repo.radeon.com/rocm/apt/debian/rocm.gpg.key | apt-key add - && \
  sh -c 'echo deb [arch=amd64] http://repo.radeon.com/rocm/apt/debian/ xenial main > /etc/apt/sources.list.d/rocm.list' && \
  apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
  sudo \
  libelf1 \
  libnuma-dev \
  build-essential \
  liblist-moreutils-perl \
  libboost-program-options-dev \
  cmake \
  git \
  unzip \
  rocm-dev && \
  apt-get clean && \
  rm -rf /var/lib/apt/lists/*

ENV PATH "${PATH}:/opt/rocm/bin"

# .NET core runtime 3.1 packages
RUN apt-get update && apt-get install -y wget \
  apt-transport-https && \
  wget -q https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb && \
  dpkg -i packages-microsoft-prod.deb && \
  apt-get update && apt-get install -y apt-transport-https && \
  apt-get update && apt-get install -y aspnetcore-runtime-3.1

# compile example application
RUN git clone https://github.com/vsrad/radeon-asm-tools.git && \
  cd radeon-asm-tools/Example/VectorAddProjectExample && \
  mkdir build && \
  cd build && \
  cmake .. && \
  make

WORKDIR /

# download Debug Server
RUN wget https://github.com/vsrad/radeon-asm-tools/releases/download/19.11.15/Release.zip && \
  unzip Release.zip && \
  rm Release.zip && \
  chmod +x Release/DebugServerLinux64/RadeonAsmDebugServer

WORKDIR Release/DebugServerLinux64