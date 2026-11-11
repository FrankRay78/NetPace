FROM ubuntu:22.04

# Populated by buildx / docker compose automatically. Defaults to amd64 for
# plain `docker build` invocations.
ARG TARGETARCH=amd64

# iproute2 supplies tc and ip, which shape.sh uses to apply a slow-link profile to
# the container's own interface. Present whether or not a profile is requested, so a
# profile is never refused for want of a tool the image could have carried.
RUN apt-get update \
 && apt-get install -y --no-install-recommends wget ca-certificates iproute2 \
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

COPY link-profiles.sh shape.sh lab-properties.sh ./

# Turn off the server's own outbound traffic, which would otherwise cross a shaped link while
# NetPace is measuring it. Build-time rather than entry-point work: the properties file is baked
# into the image, so doing it here also means a setting the upstream file no longer names fails
# the build.
RUN bash lab-properties.sh OoklaServer.properties

EXPOSE 8080/tcp 8080/udp

# Loopback, deliberately: shaping is applied to eth0, so the healthcheck stays fast
# and honest on every profile and reports on OoklaServer itself rather than the link.
HEALTHCHECK --interval=10s --timeout=3s --start-period=10s --retries=3 \
  CMD wget -q --spider http://localhost:8080/speedtest/latency.txt || exit 1

# Invoked through bash rather than directly, so the entry point does not depend on the
# executable bit surviving the checkout the image is built from.
ENTRYPOINT ["bash", "/opt/ookla/shape.sh"]

CMD ["./OoklaServer"]
