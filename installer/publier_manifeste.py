# -*- coding: utf-8 -*-
r"""Genere le manifeste de mise a jour SIGNE (latest.json) pour WinLog Analyzer.

Ne publie rien : produit dist/installer/latest.json a joindre manuellement
a la release GitHub (avec WinLogAnalyzer-Setup-X.Y.Z.exe).

Signature : ECDSA P-256 / SHA-256, format brut r||s (celui de .NET ECDsa.VerifyData).
Cle privee : D:\MDP\loganalyzer-updater.key (PEM PKCS8), jamais dans le depot.

Usage :
    python installer/publier_manifeste.py --genkey      # une seule fois ; affiche la cle publique
    python installer/publier_manifeste.py --notes "Phrase pour l'utilisateur."
"""
import argparse
import base64
import hashlib
import json
import re
import sys
from pathlib import Path

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec
from cryptography.hazmat.primitives.asymmetric.utils import decode_dss_signature

RACINE = Path(__file__).resolve().parents[1]
CLE = Path(r"D:\MDP\loganalyzer-updater.key")
BASE = "https://github.com/Heiphaistos/Log-Analyzer-AppWindows/releases/download"


def pub_b64(priv):
    der = priv.public_key().public_bytes(serialization.Encoding.DER,
                                         serialization.PublicFormat.SubjectPublicKeyInfo)
    return base64.b64encode(der).decode()


def genkey():
    if CLE.exists():
        sys.exit("refus : %s existe deja (ne jamais ecraser)." % CLE)
    priv = ec.generate_private_key(ec.SECP256R1())
    CLE.write_bytes(priv.private_bytes(serialization.Encoding.PEM,
                                       serialization.PrivateFormat.PKCS8,
                                       serialization.NoEncryption()))
    print("Cle privee ecrite :", CLE)
    print("Cle publique (a mettre dans AppUpdater.PublicKeyBase64) :", pub_b64(priv))


def message(version, url, size, sha, notes):
    # DOIT rester identique a AppUpdater.SignedMessage (C#).
    return ("winlog-update-v1\n%s\n%s\n%d\n%s\n%s" % (version, url, size, sha, notes)).encode("utf-8")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--genkey", action="store_true")
    ap.add_argument("--notes", default="")
    a = ap.parse_args()
    if a.genkey:
        return genkey()
    version = re.search(r'<Version>([^<]+)</Version>',
                        (RACINE / "src/WinLogAnalyzer.App/WinLogAnalyzer.App.csproj").read_text(encoding="utf-8")).group(1)
    nom = "WinLogAnalyzer-Setup-%s.exe" % version
    setup = RACINE / "dist" / "installer" / nom
    if not setup.exists():
        sys.exit("introuvable : %s (iscc installer/setup.iss d'abord)" % setup)
    data = setup.read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    url = "%s/v%s/%s" % (BASE, version, nom)
    priv = serialization.load_pem_private_key(CLE.read_bytes(), None)
    r, s = decode_dss_signature(priv.sign(message(version, url, len(data), sha, a.notes),
                                          ec.ECDSA(hashes.SHA256())))
    sig = base64.b64encode(r.to_bytes(32, "big") + s.to_bytes(32, "big")).decode()
    out = setup.with_name("latest.json")
    out.write_text(json.dumps({"version": version, "url": url, "size": len(data), "sha256": sha,
                               "notes": a.notes, "signature": sig}, ensure_ascii=False, indent=2),
                   encoding="utf-8")
    print("Ecrit :", out)
    print("A joindre a la release v%s : %s + latest.json" % (version, nom))


if __name__ == "__main__":
    main()
