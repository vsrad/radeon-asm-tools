FROM ubuntu:16.04

RUN apt-get update && \
  DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends curl apt-transport-https ca-certificates && \
  # add ROCm repository
  curl -sL http://repo.radeon.com/rocm/apt/debian/rocm.gpg.key | apt-key add - && \
  sh -c 'echo deb [arch=amd64] http://repo.radeon.com/rocm/apt/debian/ xenial main > /etc/apt/sources.list.d/rocm.list' && \
  # add .NET Core repository (required for Debug Server)
  curl -O https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb && \
  dpkg -i packages-microsoft-prod.deb && \
  rm packages-microsoft-prod.deb && \
  # install development tools, dependencies, and ROCm
  apt-get update && \
  DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
  libelf1 \
  libnuma-dev \
  build-essential \
  liblist-moreutils-perl \
  cmake \
  unzip \
  rocm-dev \
  dotnet-runtime-3.1 && \
  apt-get clean && \
  rm -rf /var/lib/apt/lists/*

ENV PATH "${PATH}:/opt/rocm/bin:/opt/rocm/llvm/bin"

ENV VERSION=2021.01.22

# compile example application
RUN curl -L https://github.com/vsrad/radeon-asm-tools/archive/${VERSION}.zip -o source.zip && \
  unzip source.zip "radeon-asm-tools-${VERSION}/Example/VectorAddProjectExample/*" && \
  mv radeon-asm-tools-${VERSION}/Example/VectorAddProjectExample . && \
  rm -r source.zip radeon-asm-tools-${VERSION} && \
  cd VectorAddProjectExample && \
  mkdir build && \
  cd build && \
  cmake .. && \
  make

# download Debug Server
RUN curl -LO https://github.com/vsrad/radeon-asm-tools/releases/download/${VERSION}/Release.zip && \
  unzip -j Release.zip 'Release/DebugServerLinux64/*' -d DebugServerLinux64 && \
  rm Release.zip && \
  chmod +x DebugServerLinux64/RadeonAsmDebugServer

WORKDIR /DebugServerLinux64
