"""Cria no `smsmarica.unidade` as duas unidades do Prime que nao existiam. ESCRITA EM PRODUCAO.

Autorizado pelo Bernardo em 23/09/2026 ("as unidades podem ser gravadas direto pelo banco").
Roda uma vez; e idempotente (`on conflict (cnes) do nothing`), entao rodar de novo nao duplica.

Por que estas duas: o Prime tem 10 unidades com historico e 8 casavam com o nosso cadastro.
CEREST (17 atendimentos) e Odontomovel (139) nao existiam - e atendimento sem unidade nao entra
no hub, porque a unidade e o eixo duravel do ADR-0039 e o CNES e a ponte entre PEPs.

CNES e endereco vieram do cadastro nacional, nao de palpite:
  GET https://apidadosabertos.saude.gov.br/cnes/estabelecimentos?codigo_municipio=330270
Os dois confirmados em Marica (IBGE 330270).

**O zero a esquerda do Odontomovel e obrigatorio.** A API devolve `209724` porque serializa o
CNES como inteiro, mas as 51 unidades ja cadastradas sao todas de 7 digitos. Gravar `209724`
faria o de-para por CNES nao casar - e a unidade sumiria em silencio, sem erro nenhum.

Os UUID sao v7 (como o app gera) e estao FIXOS no codigo de proposito: assim rodar duas vezes
nao cria linha nova nem por acidente, e o id fica rastreavel a este script.
"""

from __future__ import annotations

import pathlib
import sys

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parents[1]
                       / "Aprendizados e Scratchpads" / "ferramentas"))
from db import conn  # noqa: E402

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

NOVAS = [
    # (id v7 fixo, nome, cnes, logradouro, numero, bairro, cep)
    ("01a0cf59-db76-7925-a60c-6a51a036c018", "ODONTOMOVEL MARICA", "0209724",
     "Av. Roberto Silveira", "46", "Centro", "24900440"),
    ("01a0cf59-db76-73bb-90a8-550e0a967554", "CEREST MARICA", "6893430",
     "Rua Jovino Duarte de Oliveira", "2142", "Aracatiba", "24901130"),
]

SQL = """
insert into smsmarica.unidade
  (id, nome, cnes, ativo, externa, criado_em,
   endereco_logradouro, endereco_numero, endereco_bairro,
   endereco_cidade, endereco_uf, endereco_cep, codigo_ibge_cidade)
values (%s, %s, %s, true, false, now(), %s, %s, %s, 'Maricá', 'RJ', %s, '330270')
on conflict (cnes) where cnes is not null do nothing
"""


def main() -> int:
    with conn() as c, c.cursor() as cur:
        for ident, nome, cnes, log, num, bairro, cep in NOVAS:
            cur.execute(SQL, (ident, nome, cnes, log, num, bairro, cep))
            estado = "inserida" if cur.rowcount else "ja existia (nada feito)"
            print(f"   {nome:<22} cnes={cnes}  -> {estado}")

    print("\n--- conferencia: relendo do banco ---")
    with conn() as c, c.cursor() as cur:
        cur.execute("""select nome, cnes, ativo, externa, endereco_logradouro, endereco_numero,
                              endereco_bairro, endereco_cidade, endereco_cep, codigo_ibge_cidade
                       from smsmarica.unidade
                       where cnes in ('0209724', '6893430') order by nome""")
        linhas = cur.fetchall()
        for r in linhas:
            print("   " + " | ".join(str(x) for x in r))
        cur.execute("select count(*) from smsmarica.unidade")
        print(f"\ntotal de unidades: {cur.fetchone()[0]}  (eram 51)")
    if len(linhas) != 2:
        print("\nATENCAO: esperava 2 linhas na conferencia — NAO seguir para a carga.")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
