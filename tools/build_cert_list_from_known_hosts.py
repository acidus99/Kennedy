#!/usr/bin/env python3

import argparse
import csv
import hashlib
import ssl
import socket
import subprocess
import sys
import tempfile
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlparse


DEFAULT_SOURCE_URL = "gemini://kennedy.gemi.dev/observatory/known-hosts"
DEFAULT_OUTPUT_PATH = "gemini-cert-list.csv"


@dataclass
class CapsuleRecord:
    host: str
    port: int
    public_key_sha256: str
    last_seen_utc: str
    times_seen: int
    not_after_utc: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build a Gemini certificate CSV from Kennedy known-hosts."
    )
    parser.add_argument(
        "--source-url",
        default=DEFAULT_SOURCE_URL,
        help=f"Gemini page containing capsule links (default: {DEFAULT_SOURCE_URL})",
    )
    parser.add_argument(
        "--output",
        default=DEFAULT_OUTPUT_PATH,
        help=f"Output CSV path (default: {DEFAULT_OUTPUT_PATH})",
    )
    parser.add_argument(
        "--timeout",
        type=float,
        default=15.0,
        help="Connection and read timeout in seconds (default: 15)",
    )
    parser.add_argument(
        "--delay-seconds",
        type=float,
        default=0.25,
        help="Delay between capsule requests in seconds (default: 0.25)",
    )
    parser.add_argument(
        "--limit",
        type=int,
        default=0,
        help="Maximum number of capsules to process, 0 means no limit (default: 0)",
    )
    return parser.parse_args()


def fetch_gemini(url: str, timeout_seconds: float) -> tuple[int, str, str, bytes]:
    parsed = urlparse(url)
    if parsed.scheme != "gemini":
        raise ValueError(f"Unsupported URL scheme: {parsed.scheme}")
    if not parsed.hostname:
        raise ValueError(f"URL is missing a hostname: {url}")

    host = parsed.hostname
    port = parsed.port or 1965

    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    context.check_hostname = False
    context.verify_mode = ssl.CERT_NONE

    request_bytes = f"{url}\r\n".encode("utf-8")

    with socket.create_connection((host, port), timeout=timeout_seconds) as sock:
        sock.settimeout(timeout_seconds)
        with context.wrap_socket(sock, server_hostname=host) as tls_sock:
            certificate_der = tls_sock.getpeercert(binary_form=True)
            tls_sock.sendall(request_bytes)

            response_bytes = bytearray()
            while True:
                chunk = tls_sock.recv(4096)
                if not chunk:
                    break
                response_bytes.extend(chunk)

    header_bytes, separator, body_bytes = bytes(response_bytes).partition(b"\r\n")
    if not separator:
        raise ValueError(f"Gemini response did not contain a CRLF header terminator: {url}")

    header_text = header_bytes.decode("utf-8", errors="replace")
    if len(header_text) < 2 or not header_text[:2].isdigit():
        raise ValueError(f"Invalid Gemini response header: {header_text!r}")

    status_code = int(header_text[:2])
    meta = header_text[3:] if len(header_text) > 3 else ""
    body_text = body_bytes.decode("utf-8", errors="replace")
    return status_code, meta, body_text, certificate_der


def extract_capsule_counts(gemtext: str) -> dict[tuple[str, int], int]:
    counts: dict[tuple[str, int], int] = {}

    for raw_line in gemtext.splitlines():
        stripped = raw_line.lstrip()
        if not stripped.startswith("=>"):
            continue

        parts = stripped[2:].strip().split(maxsplit=1)
        if not parts:
            continue

        parsed = urlparse(parts[0])
        if parsed.scheme != "gemini" or not parsed.hostname:
            continue

        key = (parsed.hostname.lower(), parsed.port or 1965)
        counts[key] = counts.get(key, 0) + 1

    return counts


def fetch_certificate_pem(host: str, port: int, timeout_seconds: float) -> str:
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
    context.check_hostname = False
    context.verify_mode = ssl.CERT_NONE
    request_bytes = f"gemini://{host}:{port}/\r\n".encode("utf-8")

    with socket.create_connection((host, port), timeout=timeout_seconds) as sock:
        sock.settimeout(timeout_seconds)
        with context.wrap_socket(sock, server_hostname=host) as tls_sock:
            certificate_der = tls_sock.getpeercert(binary_form=True)
            tls_sock.sendall(request_bytes)

            response_line = bytearray()
            while not response_line.endswith(b"\r\n"):
                chunk = tls_sock.recv(1)
                if not chunk:
                    break
                response_line.extend(chunk)

    return ssl.DER_cert_to_PEM_cert(certificate_der)


def get_public_key_sha256_and_not_after(certificate_pem: str) -> tuple[str, str]:
    with tempfile.NamedTemporaryFile("w", suffix=".pem", delete=True) as cert_file:
        cert_file.write(certificate_pem)
        cert_file.flush()

        pubkey_pem = subprocess.run(
            ["openssl", "x509", "-in", cert_file.name, "-pubkey", "-noout"],
            check=True,
            capture_output=True,
            text=True,
        ).stdout

        pubkey_der = subprocess.run(
            ["openssl", "pkey", "-pubin", "-outform", "DER"],
            input=pubkey_pem.encode("utf-8"),
            check=True,
            capture_output=True,
        ).stdout

        enddate_output = subprocess.run(
            ["openssl", "x509", "-in", cert_file.name, "-noout", "-enddate"],
            check=True,
            capture_output=True,
            text=True,
        ).stdout.strip()

    public_key_sha256 = hashlib.sha256(pubkey_der).hexdigest()
    not_after_utc = parse_openssl_enddate(enddate_output)
    return public_key_sha256, not_after_utc


def parse_openssl_enddate(enddate_output: str) -> str:
    prefix = "notAfter="
    if not enddate_output.startswith(prefix):
        raise ValueError(f"Unexpected openssl enddate output: {enddate_output!r}")

    raw_value = enddate_output[len(prefix):]
    not_after = datetime.strptime(raw_value, "%b %d %H:%M:%S %Y %Z")
    return not_after.replace(tzinfo=timezone.utc).isoformat().replace("+00:00", "Z")


def write_csv(records: list[CapsuleRecord], output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)

    with output_path.open("w", newline="", encoding="utf-8") as handle:
        writer = csv.writer(handle)
        writer.writerow(
            [
                "host",
                "port",
                "public_key_sha256",
                "last_seen_utc",
                "times_seen",
                "not_after_utc",
            ]
        )

        for record in records:
            writer.writerow(
                [
                    record.host,
                    record.port,
                    record.public_key_sha256,
                    record.last_seen_utc,
                    record.times_seen,
                    record.not_after_utc,
                ]
            )


def build_records(source_url: str, timeout_seconds: float, delay_seconds: float, limit: int) -> list[CapsuleRecord]:
    status_code, meta, body, _ = fetch_gemini(source_url, timeout_seconds)
    if status_code // 10 != 2:
        raise RuntimeError(f"Known-hosts request failed: {status_code} {meta}")

    capsule_counts = extract_capsule_counts(body)
    if not capsule_counts:
        raise RuntimeError("No Gemini capsule links were found on the known-hosts page.")

    capsule_items = sorted(capsule_counts.items())
    if limit > 0:
        capsule_items = capsule_items[:limit]

    records: list[CapsuleRecord] = []
    total = len(capsule_items)

    for index, ((host, port), times_seen) in enumerate(capsule_items, start=1):
        print(f"[{index}/{total}] Fetching certificate for {host}:{port}", file=sys.stderr)

        try:
            certificate_pem = fetch_certificate_pem(host, port, timeout_seconds)
            public_key_sha256, not_after_utc = get_public_key_sha256_and_not_after(certificate_pem)
            last_seen_utc = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

            records.append(
                CapsuleRecord(
                    host=host,
                    port=port,
                    public_key_sha256=public_key_sha256,
                    last_seen_utc=last_seen_utc,
                    times_seen=times_seen,
                    not_after_utc=not_after_utc,
                )
            )
        except Exception as exc:
            print(f"Skipping {host}:{port}: {exc}", file=sys.stderr)

        time.sleep(delay_seconds)

    return records


def main() -> int:
    args = parse_args()

    try:
        records = build_records(args.source_url, args.timeout, args.delay_seconds, args.limit)
        write_csv(records, Path(args.output))
    except subprocess.CalledProcessError as exc:
        print(f"OpenSSL command failed: {exc}", file=sys.stderr)
        return 1
    except Exception as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1

    print(f"Wrote {len(records)} certificate rows to {args.output}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
