"""Runs a command with stdout attached to a real pseudo-terminal and copies what it wrote to our stdout.
Usage: python3 -I pty-run.py <command> [args...]
Exit 125 (message on stderr) if no pty could be allocated; otherwise the child's exit status."""
import os
import subprocess
import sys

try:
    master, slave = os.openpty()
except OSError as e:
    sys.stderr.write("pty-run: cannot allocate a pty: %s\n" % e)
    sys.exit(125)

proc = subprocess.Popen(sys.argv[1:], stdin=subprocess.DEVNULL, stdout=slave, stderr=subprocess.DEVNULL, close_fds=True)
os.close(slave)
chunks = []
while True:
    try:
        data = os.read(master, 4096)
    except OSError:  # EIO when the child side is closed
        break
    if not data:
        break
    chunks.append(data)
code = proc.wait()
sys.stdout.buffer.write(b"".join(chunks))
sys.exit(code)
