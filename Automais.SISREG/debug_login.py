import os, pathlib, re
from dotenv import load_dotenv
from sisreg.client import SisregClient, hash_senha

load_dotenv()
u = os.getenv("SISREG_USUARIO", "").strip()
s = os.getenv("SISREG_SENHA", "")
base = os.getenv("SISREG_BASE_URL", "https://sisregiii.saude.gov.br").strip()

cap = pathlib.Path(__file__).parent / "capturas"
cap.mkdir(exist_ok=True)

cli = SisregClient(base_url=base)
g = cli.priming()
print("priming:", g.status_code, "cookies:", dict(cli.cookies))

data = {"usuario": u.upper(), "senha": "", "senha_256": hash_senha(s), "etapa": "ACESSO", "logout": ""}
print("POST data keys:", {k: (v if k != 'senha_256' else v[:10]+'…') for k, v in data.items()})
r = cli.post("/", data=data)
print("post:", r.status_code, "url_final:", r.url, "len:", len(r.text))
(cap / "post_login_raw.html").write_text(r.text, encoding="utf-8")

# procurar mensagens/alertas/captcha
for pat in [r"alert\s*\(([^)]*)\)", r'class="[^"]*erro[^"]*"[^>]*>([^<]+)', r"g-recaptcha", r"hcaptcha", r"Mensagem[^<]*", r"inv[aá]lid", r"bloque", r"senha", r"tentativa"]:
    for m in re.findall(pat, r.text, re.IGNORECASE)[:5]:
        print("  match", repr(pat), "->", repr(m)[:120])
