FROM rocm/dev-ubuntu-18.04:4.3.1

USER root

RUN apt-get update && \
  DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends curl apt-transport-https ca-certificates && \
  # add .NET Core repository (required for Debug Server)
  curl -O https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb && \
  dpkg -i packages-microsoft-prod.deb && \
  rm packages-microsoft-prod.deb && \
  # install development tools, dependencies
  apt-get update && \
  DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
  liblist-moreutils-perl \
  cmake \
  unzip \
  dotnet-runtime-3.1 && \
  apt-get clean && \
  rm -rf /var/lib/apt/lists/*

ENV PATH "${PATH}:/opt/rocm/bin:/opt/rocm/llvm/bin"

WORKDIR /

ENV VERSION=2021.03.03

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
