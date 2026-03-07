#!/bin/bash
set -euo pipefail

# Tunel reverso dedicado para o portal DDS StudyOS.
# Nao reutiliza o manager legado do outro portal.

AWS_HOST="ubuntu@177.71.165.60"
SSH_KEY="/home/kika/dds-key.pem"
LOCAL_PORT="5081"
REMOTE_PORT="5081"

AUTOSSH_GATETIME=0 autossh \
  -M 0 \
  -N \
  -f \
  -T \
  -o ServerAliveInterval=30 \
  -o ServerAliveCountMax=3 \
  -o ExitOnForwardFailure=yes \
  -o StrictHostKeyChecking=accept-new \
  -o ConnectTimeout=30 \
  -o TCPKeepAlive=yes \
  -i "$SSH_KEY" \
  -R "${REMOTE_PORT}:localhost:${LOCAL_PORT}" \
  "$AWS_HOST"
