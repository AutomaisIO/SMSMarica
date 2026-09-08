"""Resolve identidade em massa no SER: CPF -> CNS (e cadastro completo), em paralelo.

## Por que isto existe

O hub FHIR tem 343.997 pacientes, dos quais 122.711 (36%) tem CPF e NAO tem CNS. O TXT do SISREG
so traz CNS. Cruzar os dois por CNS deixaria essa terca parte invisivel -- e importar assim criaria
uma segunda ficha para cada um. Medido em 07/09/2026: ate 37.976 duplicatas em potencial, 24.035
delas exatamente nesse caso.

Casar por NOME nao serve de desempate: o hub tem 39 pacientes chamados LUIZ CARLOS DA SILVA.

O SER resolve isso porque, perguntado pelo CPF, devolve o CNS -- e junto o cadastro inteiro
(nascimento, nome da mae, endereco, telefone, sexo). Testado em 5 casos: 4 CNS identicos ao do
SISREG e 1 divergente por ser CNS PROVISORIO no SISREG contra o DEFINITIVO no SER, que o proprio
SER sinaliza. Ou seja: o SER tambem corrige o caso em que casar por CNS erraria.

## Consulta, nao escrita

Usa o botao "Pesquisar" do painel do paciente (`<a title="Pesquisar">`), que re-renderiza o painel
via A4J. Nao grava nada. A trava de somente-leitura do laboratorio continua valendo.

## Retomavel

A saida e JSONL append-only e as chaves ja resolvidas sao puladas na largada. Cair no meio, ou
parar de proposito, nao custa nada -- religar continua de onde parou.
"""
from __future__ import annotations

import argparse
import json
import pathlib
import queue
import re
import sys
import threading
import time

import httpx
from dotenv import load_dotenv

sys.stdout.reconfigure(encoding="utf-8", errors="replace", line_buffering=True)

RAIZ = pathlib.Path(__file__).parent
load_dotenv(RAIZ / ".env")
sys.path.insert(0, str(RAIZ))

from probe_campos_dinamicos import BASE, UA, Editar, hidden_do_form, login_e_modulo, sopa  # noqa: E402

CAMPO = "form0:numeroCADSUS"
PAINEL = "form0:painelDadosDoPaciente"

# So o que interessa para identidade e cadastro. O painel devolve dezenas de campos de UI.
CAMPOS = {
    "form0:nome": "nome",
    "form0:cpf": "cpf",
    "form0:cns": "cns",
    "form0:dataNascimento": "nascimento",
    "form0:nomeMae": "nome_mae",
    "form0:sexo": "sexo",
    "form0:logradouro": "logradouro",
    "form0:numero": "numero",
    "form0:complemento": "complemento",
    "form0:bairro": "bairro",
    "form0:cep": "cep",
    "form0:municipio": "municipio",
    "form0:uf": "uf",
    "form0:raca": "raca",
}


def botao_pesquisar(html: str) -> str | None:
    for m in re.finditer(r'<a[^>]*title="Pesquisar"[^>]*>', html):
        if (idm := re.search(r'id="([^"]+)"', m.group(0))):
            return idm.group(1)
    return None


class SessaoSer:
    """Uma sessao do SER com o formulario de edicao aberto, pronta para consultas repetidas.

    O painel e reaberto uma vez e reaproveitado: refazer login a cada consulta transformaria o
    login no gargalo -- foi o que o probe fazia, e ele existe para uma consulta so.
    """

    def __init__(self, idx: int):
        self.idx = idx
        self.c = httpx.Client(base_url=BASE, headers={"User-Agent": UA}, timeout=90,
                              follow_redirects=True, verify=False)
        self.ed = None
        self.botao = None
        self.consultas = 0

    def abrir(self) -> None:
        login_e_modulo(self.c)
        self.ed = Editar(self.c)
        self.ed.abrir()
        self.botao = botao_pesquisar(self.ed.full)
        if not self.botao:
            raise RuntimeError("botao Pesquisar nao encontrado — layout mudou?")

    def consultar(self, chave: str, reabrir: bool = False) -> dict | None:
        # O JSF invalida o ViewState a cada postagem: reusar o formulario aberto uma vez fazia a
        # consulta seguinte voltar sem painel. Medido em 07/09/2026: 10 acertos em 60 tentativas
        # reusando; reabrindo, a taxa sobe. Custa uma requisicao a mais e o SER nao tem teto.
        if reabrir:
            self.ed = Editar(self.c)
            self.ed.abrir()
        dados = hidden_do_form(self.ed.full)
        dados[CAMPO] = chave
        dados |= {
            "AJAXREQUEST": "_viewRoot",
            self.botao: self.botao,
            "ajaxSingle": self.botao,
            "AJAX:EVENTS_COUNT": "1",
            "javax.faces.ViewState": self.ed.vs or "",
        }
        r = self.c.post(self.ed.act, data=dados, headers={"X-Requested-With": "XMLHttpRequest"})
        self.consultas += 1
        html = r.text
        if (m := re.search(r'<meta name="Location" content="([^"]+)"', html)):
            html = self.c.get(m.group(1).replace("&amp;", "&")).text

        painel = sopa(html).find(id=PAINEL)
        if painel is None:
            return None

        out: dict = {}
        for el in painel.find_all(["input", "select", "textarea"]):
            nome = el.get("name") or el.get("id") or ""
            if nome not in CAMPOS:
                continue
            if el.name == "select":
                sel = el.find("option", selected=True)
                valor = sel.get_text(strip=True) if sel else ""
            else:
                valor = el.get("value") or ""
            out[CAMPOS[nome]] = valor.strip()

        # NAO existe sinal legivel de "CNS provisorio x definitivo" nesta tela. O balao
        # `cns-alert-balloon` e o fundo amarelo do campo (#fff9c4) estao SEMPRE no HTML, exibidos
        # por CSS: medido em 07/09/2026 comparando um caso que bateu exato com um divergente --
        # estilo e balao identicos nos dois, e a deteccao por texto dava 100% em 8.308 registros.
        # Um flag que nunca varia e pior que flag nenhum, porque alguem vai confiar nele.
        #
        # A divergencia se apura onde ela de fato existe: comparando o CNS que o SER devolveu com o
        # CNS que o SISREG trouxe, na conciliacao. Isso e dado, nao dica de interface.

        # GUARDA DE IDENTIDADE. A producao ja registrou que o risco que mataria a paralelizacao e
        # uma sessao receber o paciente da outra: "velocidade que troca identidade de paciente e o
        # pior defeito possivel aqui" (PreCargaCadastroSerService). Cookie jar por worker DEVERIA
        # bastar -- mas "deveria" nao e verificacao, e um cruzamento seria descoberto meses depois,
        # com o cadastro errado ja gravado no hub.
        #
        # A conferencia e contra o campo CORRESPONDENTE A CHAVE perguntada. O painel aceita CNS ou
        # CPF no mesmo campo, e comparar sempre o CPF -- que era o que esta guarda fazia -- so
        # funciona quando se pergunta por CPF. Perguntando por CNS, a comparacao daria falso em
        # 100% dos casos e o resolvedor descartaria o lote inteiro como divergente.
        digitos = "".join(ch for ch in chave if ch.isdigit())
        campo = "cns" if len(digitos) == 15 else "cpf"
        devolvido = "".join(ch for ch in (out.get(campo) or "") if ch.isdigit())

        # CNS PROVISORIO -> DEFINITIVO nao e troca de pessoa, e a MESMA pessoa com o cadastro
        # regularizado. O provisorio vive na faixa que comeca por 898 (CNS de uso temporario, dado
        # a quem ainda nao tem o definitivo); o SER guarda o definitivo e devolve ele.
        #
        # Medido em 07/09/2026, no comeco da fase 2: 61 das 78 primeiras "divergencias" eram
        # exatamente isso. Tratar como troca descartaria dado valido em massa -- 5,9% do lote --
        # e, pior, esconderia as trocas de verdade no meio do ruido.
        #
        # A conversao e REGISTRADA, nao ignorada: quem concilia precisa saber que o CNS do SISREG
        # nao e o mesmo que vai para o hub, senao o proximo cruzamento por CNS erra de novo.
        provisorio_virou_definitivo = (
            campo == "cns" and digitos.startswith("898") and not devolvido.startswith("898"))

        if provisorio_virou_definitivo:
            out["cns_provisorio_origem"] = digitos
            out["cns_definitivo"] = devolvido
        # Campo vazio ou de preenchimento (000.000.000-00) nao e troca: e ficha incompleta na
        # origem -- 25 em 1.936 na medicao de 07/09/2026. So valor REAL e diferente acusa.
        elif devolvido and set(devolvido) != {"0"} and devolvido != digitos:
            out["ALERTA_IDENTIDADE"] = f"pedi {campo.upper()} {digitos}, veio {devolvido}"
        return out or None

    def fechar(self) -> None:
        try:
            self.c.close()
        except Exception:
            pass


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--entrada", required=True, help="arquivo com uma chave (CPF ou CNS) por linha")
    ap.add_argument("--saida", required=True, help="JSONL append-only")
    ap.add_argument("--instancias", type=int, default=10)
    ap.add_argument("--limite", type=int, default=0, help="0 = todas")
    args = ap.parse_args(argv)

    entrada = pathlib.Path(args.entrada)
    saida = pathlib.Path(args.saida)
    saida.parent.mkdir(parents=True, exist_ok=True)

    chaves = [l.strip() for l in entrada.read_text(encoding="utf-8").splitlines() if l.strip()]

    ja = set()
    if saida.exists():
        for linha in saida.open(encoding="utf-8"):
            try:
                ja.add(json.loads(linha)["chave"])
            except (ValueError, KeyError):
                pass
    pendentes = [k for k in chaves if k not in ja]
    if args.limite:
        pendentes = pendentes[:args.limite]

    print(f"entrada: {len(chaves):,} chaves · ja resolvidas: {len(ja):,} · "
          f"a resolver agora: {len(pendentes):,}")
    if not pendentes:
        return 0

    fila: queue.Queue = queue.Queue()
    for k in pendentes:
        fila.put(k)

    trava = threading.Lock()
    fh = saida.open("a", encoding="utf-8")
    contas = {"ok": 0, "vazio": 0, "erro": 0, "identidade_divergente": 0}
    t0 = time.monotonic()

    def trabalhador(idx: int) -> None:
        s = SessaoSer(idx)
        try:
            s.abrir()
        except Exception as e:
            print(f"  [{idx}] falhou ao abrir sessao: {e}")
            return
        print(f"  [{idx}] sessao aberta")

        while True:
            try:
                chave = fila.get_nowait()
            except queue.Empty:
                break
            try:
                d = s.consultar(chave, reabrir=True)
                if d and d.get("ALERTA_IDENTIDADE"):
                    # NAO entra como resolvido -- identidade trocada e pior que identidade ausente
                    # --, mas o payload INTEIRO fica guardado. A divergencia so vira reporte se a
                    # evidencia sobreviver: sem o nome e o nascimento que o SER devolveu, nao da
                    # para mostrar a quem investiga POR QUE aquilo era outra pessoa.
                    estado = "identidade_divergente"
                    reg = {"chave": chave, "estado": estado, "alerta": d["ALERTA_IDENTIDADE"], **d}
                    print(f"  !! IDENTIDADE DIVERGENTE: {d['ALERTA_IDENTIDADE']}")
                else:
                    estado = "ok" if d else "vazio"
                    reg = {"chave": chave, "estado": estado, **(d or {})}
            except Exception as e:
                estado = "erro"
                reg = {"chave": chave, "estado": "erro", "erro": str(e)[:200]}
                # Sessao pode ter caido; tenta reabrir uma vez para nao perder o trabalhador.
                try:
                    s.abrir()
                except Exception:
                    pass

            with trava:
                fh.write(json.dumps(reg, ensure_ascii=False) + "\n")
                fh.flush()
                contas[estado] += 1
                feitos = sum(contas.values())
                if feitos % 100 == 0:
                    seg = time.monotonic() - t0
                    taxa = feitos / seg if seg else 0
                    resta = (len(pendentes) - feitos) / taxa / 60 if taxa else 0
                    print(f"  {feitos:,}/{len(pendentes):,}  ok={contas['ok']:,} "
                          f"vazio={contas['vazio']:,} erro={contas['erro']:,}  "
                          f"{taxa:.1f}/s  restam ~{resta:.0f} min")
        s.fechar()

    threads = [threading.Thread(target=trabalhador, args=(i + 1,), daemon=True)
               for i in range(args.instancias)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()

    fh.close()
    seg = time.monotonic() - t0
    print(f"\n=== FIM === {sum(contas.values()):,} em {seg/60:.1f} min "
          f"({sum(contas.values())/seg:.1f}/s)")
    print(f"  ok: {contas['ok']:,}   vazio: {contas['vazio']:,}   erro: {contas['erro']:,}")
    if contas["identidade_divergente"]:
        print(f"  !! IDENTIDADE DIVERGENTE: {contas['identidade_divergente']:,} — "
              f"investigar ANTES de usar qualquer coisa deste lote")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
