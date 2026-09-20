"""Cancela uma solicitação no SISREG — a partir da fila de Cancelamento do SMSMais.

O formulário está documentado em docs/APRENDIZADOS.md ("CANCELAR AGENDAMENTO"), lido de uma
captura real da extensão. Aqui ele é reproduzido pelo backend, sem navegador.

    LISTAR              POST /cgi-bin/cons_verificar  etapa=LISTAR, cns=<CNS>, pg=<pagina>
    EXCLUIR_SOLICITACAO POST /cgi-bin/cons_verificar  etapa=EXCLUIR_SOLICITACAO, chk_<N>=<codigo>,
                                                      justificativa=<texto>, nr_pagina=<pagina>

REGRA QUE NÃO SE NEGOCIA: nunca adivinhar o índice do checkbox. `chk_<N>` mistura a POSIÇÃO da
linha na página com o VALOR (o código da solicitação), e não se sabe qual dos dois o CGI usa.
Então o caminho é o mesmo do navegador: listar, achar no HTML a linha que contém o código, e usar
o nome e o valor EXATOS daquele checkbox. Assim as duas hipóteses dão no mesmo resultado.

E a confirmação nunca é o HTTP 200: é a releitura. Depois de excluir, lista de novo e prova que a
solicitação saiu.

Uso:
    python cancelar_solicitacao.py --fila            # mostra a fila de Cancelamento do SMSMais
    python cancelar_solicitacao.py --codigo 123456   # LÊ o SISREG e mostra o que seria enviado
    python cancelar_solicitacao.py --codigo 123456 --executar --justificativa "..."

Sem --executar nada é escrito. As credenciais saem do .env (SISREG_USUARIO/SISREG_SENHA) — este
script não pede senha e não a imprime.
"""
from __future__ import annotations

import argparse
import html as _html
import json
import os
import pathlib
import re
import sys

import httpx
from dotenv import load_dotenv

sys.path.insert(0, str(pathlib.Path(__file__).parent))
from sisreg.client import SisregClient  # noqa: E402

CAMINHO = "/cgi-bin/cons_verificar"


class SessaoExpirada(RuntimeError):
    """A sessão salva morreu. Renovar DERRUBA quem estiver usando o mesmo operador."""


# ---------------------------------------------------------------- fila do SMSMais

def conectar_smsmais():
    import psycopg2
    p = os.path.join(os.environ["APPDATA"], "Microsoft", "UserSecrets", "smsmarica-api-dev", "secrets.json")
    cs = json.load(open(p, encoding="utf-8-sig"))["ConnectionStrings:DefaultDb"]
    d = {k.strip().lower(): v for k, v in (x.split("=", 1) for x in cs.split(";") if "=" in x)}
    return psycopg2.connect(host=d["host"], port=d["port"], dbname=d["database"],
                            user=d.get("username") or d.get("user id"), password=d["password"],
                            sslmode="require")


def fila(codigo: str | None = None):
    """Fichas em que o paciente pediu cancelamento e ninguém tratou."""
    cur = conectar_smsmais().cursor()
    cur.execute("""
        select s.codigo_solicitacao, p.cns, p.nome,
               (s.data_agendada at time zone 'America/Sao_Paulo')::timestamp,
               coalesce(s.procedimento_texto, s.especialidade_texto, ''),
               coalesce(u.nome, ''), s.confirmado_canal,
               coalesce(s.motivo_cancelamento_paciente, '')
          from smsmarica.solicitacao s
          join fhir.patient p on p.id = s.paciente_id
          left join smsmarica.unidade u on u.id = s.unidade_executante_id
         where s.excluido_em is null and s.status <> 4 and s.status_confirmacao = 3
           and s.data_agendada >= date_trunc('day', now() at time zone 'America/Sao_Paulo')
           and (%s is null or s.codigo_solicitacao = %s)
         order by s.data_agendada
    """, (codigo, codigo))
    return cur.fetchall()


# ---------------------------------------------------------------- SISREG

def listar(cli: SisregClient, cns: str, pg: int = 0, nr_pagina: str = "", ordem: str = "",
           total: str = "") -> str:
    """Uma página da listagem daquele CNS. Devolve o HTML."""
    r = cli.post(CAMINHO, data={
        "pg": str(pg), "cns": cns, "etapa": "LISTAR", "ordem": ordem, "total": total,
        "co_solic": "", "dt_final": "", "nr_pagina": nr_pagina, "dt_inicial": "",
        "codigo_solicitacao": "",
    })
    r.raise_for_status()
    return r.text


def achar_linha(html: str, codigo: str) -> tuple[str, str] | None:
    """
    (nome_do_checkbox, valor) da linha que contém o código — lidos do HTML, nunca inventados.
    Só aceita se o VALOR for exatamente o código procurado.
    """
    for m in re.finditer(r"""<input[^>]*type=["']?checkbox["']?[^>]*>""", html, re.I):
        tag = m.group(0)
        nome = re.search(r"""name=["']?(chk_\d+)["']?""", tag, re.I)
        valor = re.search(r"""value=["']?(\d+)["']?""", tag, re.I)
        if nome and valor and valor.group(1) == str(codigo):
            return nome.group(1), valor.group(1)
    return None


def total_e_ordem(html: str) -> tuple[str, str]:
    t = re.search(r"""name=["']?total["']?[^>]*value=["']?(\d*)""", html, re.I)
    o = re.search(r"""name=["']?ordem["']?[^>]*value=["']?(\d*)""", html, re.I)
    return (t.group(1) if t else ""), (o.group(1) if o else "")


def procurar(cli: SisregClient, cns: str, codigo: str, max_paginas: int = 12):
    """Varre as páginas daquele CNS até achar a linha. Devolve (pagina, nome, valor, html)."""
    html = listar(cli, cns)
    if not sessao_viva(html):
        raise SessaoExpirada()
    total, ordem = total_e_ordem(html)
    achado = achar_linha(html, codigo)
    if achado:
        return 0, achado[0], achado[1], html

    pagina = 0
    for _ in range(max_paginas):
        proxima = pagina + 1
        html = listar(cli, cns, pg=proxima, nr_pagina=str(pagina), ordem=ordem or "1", total=total)
        achado = achar_linha(html, codigo)
        if achado:
            return proxima, achado[0], achado[1], html
        if f"chk_" not in html:      # acabaram as linhas
            break
        pagina = proxima
    return None, None, None, html


SESSAO_MORTA = re.compile(r"sess[a&][o;ã]?[^<]{0,40}expirou|Tempo de inatividade|Efetue o logon", re.I)


def sessao_viva(html: str) -> bool:
    """
    O SISREG devolve HTTP 200 com uma página de erro quando a sessão morreu, então o status não
    serve de prova. E `SisregClient.esta_logado()` dá FALSO POSITIVO aqui: o cookie velho passa
    na checagem dele e só a requisição de verdade revela a morte. Por isso a checagem é no corpo.
    """
    return not SESSAO_MORTA.search(html)


def alerta(html: str) -> str | None:
    m = re.search(r"""alert\s*\(\s*['"](.{0,200}?)['"]\s*\)""", html, re.S)
    return m.group(1).strip() if m else None


def _texto(html_bruto: str) -> str:
    """HTML -> texto plano, COM as entidades resolvidas.

    O SISREG escreve os acentos como entidades (`Situa&ccedil;&atilde;o Atual`). Sem desfazê-las,
    qualquer busca por "Situação" não casa — foi assim que a ficha certa passou por "não achei".
    """
    t = re.sub(r"<script.*?</script>", " ", html_bruto, flags=re.S | re.I)
    t = re.sub(r"<[^>]+>", " ", t)
    return re.sub(r"\s+", " ", _html.unescape(t)).strip()


def situacao(cli: SisregClient, codigo: str) -> str | None:
    """
    Situação da solicitação, lida da FICHA — aberta DIRETO pelo código, sem listagem nenhuma.

    Esta é a verificação que vale. A anterior ("sumiu da listagem") era ausência de prova virando
    prova: em 20/09 ela aprovou um cancelamento e, no mesmo caminho, "aprovou" também uma
    solicitação que ninguém tinha cancelado — a listagem voltava vazia para tudo. A ficha diz o
    estado em vez de deixar deduzir: `AGENDAMENTO / CANCELADO / REGULADOR`.
    """
    telas = (
        ("/cgi-bin/cons_marcados_reg", {"etapa": "EXIBIR_FICHA", "co_solicitacao": str(codigo)}, "post"),
        ("/cgi-bin/gerenciador_solicitacao",
         {"etapa": "VISUALIZAR_FICHA", "co_seq_solicitacao": str(codigo)}, "get"),
    )
    # A ficha às vezes volta sem os dados (a página monta, o miolo vem vazio) — o mesmo sintoma de
    # quando outra sessão do operador está ativa. Como aqui a resposta É a prova, insiste-se: duas
    # voltas pelas duas telas antes de admitir que não sabe.
    for _ in range(2):
        for caminho, dados, metodo in telas:
            r = cli.post(caminho, data=dados) if metodo == "post" else cli.get(caminho, params=dados)
            r.raise_for_status()
            if not sessao_viva(r.text):
                raise SessaoExpirada()
            texto = _texto(r.text)
            # A ficha é uma tabela: uma linha de RÓTULOS e a seguinte com os VALORES. Procurar logo
            # depois de "Situação Atual:" pega o rótulo vizinho ("Data de Cancelamento"), não o
            # valor. A âncora confiável é o próprio código, que na linha de valores vem imediatamente
            # antes da situação: "689974708 AGENDAMENTO / CANCELADO / REGULADOR 20/09/2026".
            m = re.search(rf"{re.escape(str(codigo))}\s+([A-ZÇÃÕÉÍÁÚ][A-ZÇÃÕÉÍÁÚ/ ]{{4,70}})", texto)
            if m:
                # O texto corrido não tem fronteira: depois da situação já vem o rótulo seguinte
                # ("CPF do Médico Solicitante"), e ele entra no casamento. A situação tem a forma
                # FASE / ESTADO / QUEM — então ficam três partes, e da última só a primeira palavra.
                partes = [p.strip() for p in re.split(r"\s*/\s*", m.group(1)) if p.strip()][:3]
                if partes:
                    partes[-1] = partes[-1].split()[0]
                return " / ".join(partes)
    # Ficha não abriu, ou o rótulo mudou. Dizer NADA é melhor que dizer "ok".
    return None


def excluir(cli: SisregClient, cns: str, nome_chk: str, valor: str, justificativa: str,
            nr_pagina: int) -> str:
    r = cli.post(CAMINHO, data={
        "pg": "", "cns": cns, nome_chk: valor, "etapa": "EXCLUIR_SOLICITACAO",
        "ordem": "", "total": "", "co_solic": "", "dt_final": "",
        "nr_pagina": str(nr_pagina), "dt_inicial": "",
        "justificativa": justificativa[:200], "codigo_solicitacao": "",
    })
    r.raise_for_status()
    return r.text


# ---------------------------------------------------------------- comando

def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--fila", action="store_true", help="lista a fila de Cancelamento do SMSMais")
    ap.add_argument("--codigo", help="código da solicitação a cancelar")
    # A justificativa fica registrada no SISREG e é lida por gente de fora do nosso sistema:
    # diz o FATO (a pessoa avisou que não vem), sem marca de origem — de onde veio o aviso é
    # assunto nosso, não da ficha do paciente.
    ap.add_argument("--justificativa", default="Paciente informou que não comparecerá")
    ap.add_argument("--executar", action="store_true", help="GRAVA no SISREG (sem isto, só mostra)")
    ap.add_argument("--nao-renovar", action="store_true",
                    help="não refaz o login se a sessão morrer (para quando um humano está usando)")
    args = ap.parse_args()

    if args.fila:
        for cod, cns, nome, quando, proc, unid, canal, motivo in fila():
            print(f'{cod} | {quando:%d/%m %H:%M} | {nome[:28]:28} | {proc[:34]:34} | "{motivo[:45]}"')
        return 0

    if not args.codigo:
        ap.print_help()
        return 2

    linhas = fila(args.codigo)
    if not linhas:
        print(f"código {args.codigo} não está na fila de Cancelamento do SMSMais — recusando.")
        return 1
    cod, cns, nome, quando, proc, unid, canal, motivo = linhas[0]
    if not cns:
        print(f"paciente sem CNS no cadastro — a busca no SISREG é por CNS. Recusando.")
        return 1

    print("=" * 78)
    print(f"ALVO   {nome}")
    print(f"       {proc} · {unid}")
    print(f"       {quando:%d/%m/%Y %H:%M} · solicitação {cod}")
    print(f"       pediu por {canal}: \"{motivo[:70]}\"")
    print("=" * 78)

    load_dotenv(pathlib.Path(__file__).parent / ".env")
    usuario, senha = os.getenv("SISREG_USUARIO", "").strip(), os.getenv("SISREG_SENHA", "")
    if not usuario or not senha:
        print("SISREG_USUARIO/SISREG_SENHA ausentes no .env.")
        return 1

    with SisregClient() as cli:
        estado = cli.conectar(usuario, senha)
        print(f"sessão SISREG: {estado}.")

        def com_sessao(fn, *a):
            """Executa relogando uma vez se a sessão tiver morrido no meio."""
            try:
                return fn(*a)
            except SessaoExpirada:
                # O SISREG autentica UMA sessão por operador: refazer o login derruba quem
                # estiver com o mesmo usuário. Decisão do operador (20/09): "quando bater fora,
                # tenta de novo". O sintoma do outro lado é traiçoeiro — a página abre, os dados
                # é que vêm vazios.
                if args.nao_renovar:
                    raise
                print("sessão expirada — refazendo o login.")
                cli.login(usuario, senha)
                cli.salvar_sessao("capturas/.sessao.json")
                return fn(*a)

        # ANTES de tentar cancelar: em que estado a solicitação está HOJE?
        # Não é zelo excessivo — na primeira leva, 2 de 3 da nossa fila já estavam canceladas
        # pela própria unidade solicitante, e o SMSMais não sabia. Tentar cancelar o que já está
        # cancelado gasta requisição do orçamento anti-robô e polui a ficha do paciente.
        try:
            antes = com_sessao(situacao, cli, cod)
        except SessaoExpirada:
            print("\nA sessão salva EXPIROU e --nao-renovar foi pedido. Nada foi feito.")
            return 1
        print(f"situação hoje no SISREG: {antes or '(não consegui ler)'}")

        if antes and "CANCELAD" in antes.upper():
            print("\nJá está cancelada no SISREG — nada a enviar.")
            print("Falta só conciliar no SMSMais (a vaga já está livre lá).")
            return 0
        if antes is None:
            print("\nNão consegui ler a situação. Sem saber o estado de partida, não escrevo.")
            return 1

        pagina, nome_chk, valor, html = com_sessao(procurar, cli, cns, cod)
        if nome_chk is None:
            print(f"\nNÃO ACHEI a solicitação {cod} na listagem desse CNS. Nada foi enviado.")
            print("(pode já ter sido cancelada, ou estar fora do recorte da tela)")
            return 1

        print(f"\nlinha encontrada na página {pagina}: {nome_chk} = {valor}")
        print("\nrequisição que SERIA enviada:")
        print(f"  POST {CAMINHO}")
        print(f"    etapa         = EXCLUIR_SOLICITACAO")
        print(f"    cns           = {cns[:4]}…{cns[-2:]}")
        print(f"    {nome_chk:13} = {valor}")
        print(f"    nr_pagina     = {pagina}")
        print(f"    justificativa = {args.justificativa[:200]!r}")

        if not args.executar:
            print("\n(modo leitura — nada foi escrito. Use --executar para gravar.)")
            return 0

        print("\nEXECUTANDO…")
        resposta = excluir(cli, cns, nome_chk, valor, args.justificativa, pagina)
        print(f"  SISREG respondeu: {alerta(resposta)!r}")

        # A prova NUNCA é o alerta nem o HTTP 200 — é reler a ficha e ver o estado.
        # "Sumiu da listagem" não serve: a listagem só mostra o que ainda é cancelável, então
        # some tanto o que EU cancelei quanto o que já estava cancelado quanto o que a sessão
        # cega não trouxe. Em 20/09 essa ambiguidade quase virou um "deu certo" inventado.
        print("\nconferindo na ficha…")
        depois = com_sessao(situacao, cli, cod)
        print(f"  situação agora: {depois or '(não consegui ler)'}")

        ok = bool(depois) and "CANCELAD" in depois.upper()
        print("  RESULTADO:", "CANCELADA ✔" if ok
              else "NÃO confirmado ✖ — trate como NÃO cancelada e confira à mão")
        return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
