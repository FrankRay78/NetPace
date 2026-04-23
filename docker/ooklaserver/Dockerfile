FROM ubuntu:22.04

# Populated by buildx / docker compose automatically. Defaults to amd64 for
# plain `docker build` invocations.
ARG TARGETARCH=amd64

RUN apt-get update \
 && apt-get install -y --no-install-recommends wget ca-certificates \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/ookla

# The upstream OoklaServer.tgz is a bundle of per-platform archives. Pick the
# static-musl Linux build for the target architecture, then extract its
# binary and default properties file at the image root.
RUN set -eux; \
    case "${TARGETARCH}" in \
      amd64) inner="OoklaServer-linux-x86_64-static-musl.tgz" ;; \
      arm64) inner="OoklaServer-linux-aarch64-static-musl.tgz" ;; \
      *) echo "Unsupported TARGETARCH: ${TARGETARCH}" >&2; exit 1 ;; \
    esac; \
    wget -q https://install.speedtest.net/ooklaserver/stable/OoklaServer.tgz -O outer.tgz; \
    tar -xzf outer.tgz "./${inner}"; \
    tar -xzf "./${inner}"; \
    rm -f outer.tgz "./${inner}"; \
    cp OoklaServer.properties.default OoklaServer.properties; \
    chmod +x OoklaServer

EXPOSE 8080/tcp 8080/udp

HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -q --spider http://localhost:8080/speedtest/latency.txt || exit 1

CMD ["./OoklaServer"]
