"""Cadastro de paciente SEM depender da busca no CadWeb — preenchendo o que já sabemos.

Por que existe: o eco do CadWeb na grade de busca depende do USUÁRIO (medido 21/09/2026 — a
mesma consulta devolve a linha "Paciente CadWeb" numa conta e "Nenhum registro encontrado" em
outra). Depender dela para uma carga em massa é frágil: sem permissão, o robô conclui "não
existe" e duplica. Aqui a tela de cadastro é aberta direto e os campos vêm de um dicionário —
do nosso hub, de um CSV, do que for.

Sequência (a mesma que a recepção faz, medida pela extensão — ver docs/APRENDIZADOS.md §10):
  1. GET  Paciente/CadastroPaciente.aspx
  2. AutoPostBack por campo que tem AutoPostBack (cada um renova o __EVENTVALIDATION)
  3. POST rbSalvar como partial postback AJAX

Uso:  python probe_cadastro_direto.py ficha.json [--unidade GUID] [--salvar]
      (sem --salvar, monta tudo e PARA antes de gravar)
"""

from __future__ import annotations

import json
import os
import re
import sys
import time

from prime.client import (APP, CAP, LIBERADOS, PrimeSession, action_do_form, campos_todos, sopa)
from prime import combos

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

URL_CAD = f"{APP}/Paciente/CadastroPaciente.aspx"
tempos: list[tuple[str, float, int]] = []


def curto(n: str) -> str:
    return n.rsplit("$", 1)[-1]


def nome_longo(html: str, sufixo: str, dentro: str | None = None) -> str | None:
    """Nome completo do controle. `dentro` restringe ao user control — a tela de cadastro tem
    TRÊS blocos de endereço com os mesmos sufixos (`rcboTipo`, `txtNumero`…), e pegar o
    primeiro que aparece escreve no bloco errado."""
    for m in re.finditer(r'name="([^"]+)"', html):
        n = m.group(1)
        if curto(n) == sufixo and (dentro is None or f"${dentro}$" in n):
            return n
    return None


def alvo_postback(html: str, sufixo: str) -> str | None:
    """RadButton não tem `name` — o alvo do postback só existe no JS."""
    for pad in (r"WebForm_PostBackOptions\('([^']+)'", r"__doPostBack\('([^']+)'"):
        for m in re.finditer(pad, html):
            if curto(m.group(1)) == sufixo:
                return m.group(1)
    return None


def blocos_delta(texto: str):
    """`len|tipo|id|conteudo|` — fatiado pelo len, porque o conteúdo contém `|`."""
    i = 0
    while i < len(texto):
        j = texto.find("|", i)
        if j < 0:
            return
        try:
            n = int(texto[i:j])
        except ValueError:
            return
        k = texto.find("|", j + 1)
        m2 = texto.find("|", k + 1)
        if k < 0 or m2 < 0:
            return
        yield texto[j + 1:k], texto[k + 1:m2], texto[m2 + 1:m2 + 1 + n]
        i = m2 + 1 + n + 1


def aplicar_delta(campos: dict, resposta: str) -> int:
    trocados = 0
    for tipo, ident, conteudo in blocos_delta(resposta):
        if tipo != "hiddenField":
            continue
        for k in list(campos):
            if k == ident or curto(k) == ident:
                campos[k] = conteudo
                trocados += 1
    return trocados


# Combos do Telerik: o valor "de verdade" mora no hidden `_ClientState`, não no input de texto.
# Medido 21/09/2026 num POST que GRAVOU — e este achado ficou escondido por horas porque o
# comparador tratava `_ClientState` como ruído e nunca o exibia:
#   rcboTipo_ClientState  {"value":"001","text":"001 - RUA","enabled":true,...}
# Sem isso o postback do combo acontece, devolve 200, e NÃO altera nada (dá para ver pelo
# tamanho da resposta, idêntico antes e depois — enquanto o de um radio muda).
def client_state(texto: str, valor: str = "", habilitado: bool = True) -> str:
    return json.dumps({"logEntries": [], "value": valor, "text": texto,
                       "enabled": habilitado, "checkedIndices": [],
                       "checkedItemsTextOverflows": False}, separators=(",", ":"))


def codigo_do_texto(texto: str) -> str:
    """`001 - RUA` -> `001`. Os combos com código embutido no rótulo levam ele no `value`."""
    m = re.match(r"\s*(\d{3,4})\s*-\s", texto or "")
    return m.group(1) if m else ""


_KEYS_TEXTO = ("validationText", "valueAsString", "valueWithPromptAndLiterals", "lastSetTextBoxValue")


def patch_cs(shipped: str, valor: str, tipo: str = "texto", codigo: str = "") -> str:
    """Reaproveita o ClientState que a TELA entregou e injeta o valor — sem inventar chave.
    A tela em branco manda cada `_ClientState` com o schema completo e valor vazio; aqui só
    preenchemos os campos de valor, na forma que o Salvar-OK usou (medido, APRENDIZADOS §13)."""
    try:
        obj = json.loads(shipped) if shipped and shipped.strip() else {}
    except Exception:
        obj = {}
    if tipo == "combo":
        obj["value"] = codigo
        obj["text"] = valor
        obj.setdefault("logEntries", [])
        obj.setdefault("checkedIndices", [])
        obj.setdefault("checkedItemsTextOverflows", False)
    elif tipo == "data":  # valor = 'AAAA-MM-DD' -> validationText 'AAAA-MM-DD-00-00-00'
        vt = valor + "-00-00-00"
        obj["validationText"] = vt
        obj["valueAsString"] = vt
        p = valor.split("-")
        if len(p) == 3:
            obj["lastSetTextBoxValue"] = f"{p[2]}/{p[1]}/{p[0]}"
        obj.setdefault("enabled", True)
    else:  # RadTextBox / RadMaskedTextBox
        for k in _KEYS_TEXTO:
            if k in obj or k in ("validationText", "valueAsString"):
                obj[k] = valor
        obj.setdefault("enabled", True)
    return json.dumps(obj, separators=(",", ":"))


def cs_data_nascimento(html: str) -> str | None:
    """O ClientState do dateInput do RadDatePicker de nascimento (há vários dateInput na tela)."""
    m = re.search(r'name="([^"]*rdpDataNascimento[^"]*dateInput_ClientState)"', html)
    return m.group(1) if m else None


def nome_client_state(html: str, sufixo: str, dentro: str | None = None) -> str | None:
    """O hidden do ClientState usa `_` no lugar de `$` (o name é igual ao id)."""
    alvo = f"_{sufixo}_ClientState"
    for m in re.finditer(r'name="([^"]+_ClientState)"', html):
        n = m.group(1)
        if n.endswith(alvo) and (dentro is None or f"_{dentro}_" in n):
            return n
    return None


def cronometrar(rotulo, fn):
    t0 = time.perf_counter()
    r = fn()
    dt = time.perf_counter() - t0
    tempos.append((rotulo, dt, len(r.text)))
    print(f"  {rotulo:<32} {dt*1000:7.0f} ms   {len(r.text):>9,} chars   status={r.status_code}")
    return r


# Campos com AutoPostBack: a tela vai ao servidor a cada um. Ordem medida na recepção —
# a UF antes do município, porque o combo de município é encadeado nela.
# Ordem medida na tela em 21/09/2026 (o cadastro que gravou). `rdpDataNascimento` e `rcboRaca`
# TAMBEM disparam postback — eu os tratava como campo simples, e por isso o servidor nunca
# enxergava o valor. Ja' `rcboTipo`/`rcbLogradouro`/`txtNumero`/`txtComplemento` NAO disparam:
# entre o Salvar que falhou e o que gravou nao houve postback nenhum, so' digitacao.
AUTOPOSTBACK = [
    ("rblNacionalidade", "nacionalidade", "0"),
    ("chkNomePaiDesconhecido", "paiDesconhecido", None),
    ("rdpDataNascimento", "dataNascimento", None),
    ("rcboRaca", "raca", None),
    ("rcboUfNascimento", "ufNascimento", None),
    ("rcboMunicipioNascimento", "municipioNascimento", None),
    ("rblSexo", "sexo", None),
    ("rblOrientacao", "orientacao", "1"),
    ("rblIdentidadeGenero", "identidadeGenero", "1"),
    ("rblEstaSituacaoRua", "situacaoRua", "1"),
    ("rblForaDaArea", "foraDaArea", "1"),
]
# Do endereco, so' estes dois vao ao servidor sozinhos (o combo de bairro depende deles).
AUTOPOSTBACK_ENDERECO = [("rcboUF", "uf"), ("rcboMunicipio", "municipio")]
# Campos simples: vão no POST final, sem postback próprio.
SIMPLES = [
    ("txtNome", "nome"), ("txtCPF", "cpf"), ("RadTextBoxCNS", "cns"),
    ("txtNomeMae", "nomeMae"),
    ("txtCelular", "celular"),
    # O checkbox "Desconhecido" MARCA e tambem ESCREVE "DESCONHECIDO" no campo de texto (e' o
    # JS da tela que faz isso). Mandar so' o checkbox deixa o campo vazio e o servidor recusa.
    ("txtNomePai", "nomePai"),
    ("radComboBoxPais", "pais"),
    # Campos-ESPELHO: o servidor os preenche sozinho quando a selecao do combo e' resolvida.
    # Na tela em branco os combos do Telerik carregam sob demanda pelos .asmx, entao mandar so'
    # o texto ("RIO DE JANEIRO") nao resolve em codigo e o validador reprova. Medido comparando
    # com um POST que gravou: la' estes vinham preenchidos, no meu vinham vazios.
    ("txtIBGEMunicipioNascimento", "ibgeMunicipioNascimento"),
    ("txtCodigoPaisNascimento", "codigoPaisNascimento"),
    ("txtNumeroProntuario", "numeroProntuario"),
    ("txtIdade", "idade"),
]

# ENDEREÇO — obrigatório, ao contrário do que supusemos. Medido 21/09/2026 comparando dois
# POSTs do MESMO cadastro com 40 s de diferença: 283 campos idênticos, e a gravação só passou
# quando `rcboTipo`, `rcbLogradouro` e `txtNumero` do `UCEndereco` vieram preenchidos. O
# validador vive DENTRO do user control, por isso não aparece nos `Page_Validators` do painel.
ENDERECO = [
    ("rcboMunicipioCodigo", "municipioCodigo"),
    ("rcboBairro", "bairro"), ("rcboTipo", "tipoLogradouro"), ("rcbLogradouro", "logradouro"),
    ("txtNumero", "numero"), ("txtComplemento", "complemento"), ("txtCEP", "cep"),
]

# Seletor de origem por campo (Prime x CadWeb). O POST que gravou mandava os sete em "Prime".
GRUPOS_ORIGEM = ["groupNomePaciente_porCPF", "groupNomeMae_porCPF", "groupNomePai_porCPF",
                 "groupCPF_porCPF", "groupCNS_porCPF", "groupDataNascimento_porCPF",
                 "groupSexo_porCPF"]


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    salvar = "--salvar" in sys.argv
    # EXPERIMENTO: manda TUDO num POST so', sem os AutoPostBacks. Se gravar, o custo cai de
    # ~10 requisicoes para 2 (GET + POST). Nunca foi testado COM o endereco preenchido — as
    # tentativas anteriores falhavam por falta dele, entao a conclusao "os postbacks sao
    # necessarios" pode estar contaminada por aquela causa.
    sem_postback = "--sem-postback" in sys.argv
    if "--unidade" in sys.argv:
        os.environ["PRIME_UNIDADE"] = sys.argv[sys.argv.index("--unidade") + 1]
    if not args:
        raise SystemExit(__doc__)
    ficha = json.load(open(args[0], encoding="utf-8"))
    print("ficha:", ", ".join(f"{k}={str(v)[:26]}" for k, v in ficha.items()), "\n")

    with PrimeSession() as s:
        t0 = time.perf_counter()
        s.entrar()
        r = cronometrar("1. GET CadastroPaciente", lambda: s.get(URL_CAD))
        html = r.text
        (CAP / "direto_form.html").write_text(html, encoding="utf-8")

        n_sm = nome_longo(html, "RadScriptManager1") or "ctl00$ctl00$RadScriptManager1"
        id_ajax = next(iter(re.findall(r'id="([^"]*RadAjaxManager1)"', html)), "")
        CAB = {"X-MicrosoftAjax": "Delta=true", "Cache-Control": "no-cache",
               "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"}
        url = action_do_form(html, URL_CAD)
        estado = campos_todos(html)
        print(f"  campos no form em branco: {len(estado)}")

        # Resolve os CÓDIGOS INTERNOS dos combos (o servidor valida por eles) pelos mesmos .asmx
        # que o navegador usa — provado idêntico ao browser (APRENDIZADOS §15). Mesma sessão.
        # Se a ficha já traz os códigos (descobertos numa sessão anterior), usa-os e NÃO chama o
        # .asmx — é o "tiro único": sessão nova só faz GET + save, sem descoberta.
        cod = dict(ficha.get("codigosInternos") or {})
        if cod:
            print("  códigos internos (pré-resolvidos da ficha):", cod)
        else:
            try:
                if ficha.get("ufNascimento"):
                    cod["rcboUfNascimento"] = combos.uf(s, ficha["ufNascimento"])
                    if ficha.get("municipioNascimento"):
                        cod["rcboMunicipioNascimento"] = combos.municipio(s, ficha["ufNascimento"], ficha["municipioNascimento"])
                if ficha.get("raca"):
                    cod["rcboRaca"] = combos.raca(s, ficha["raca"])
                if ficha.get("uf"):
                    cod["rcboUF"] = combos.uf(s, ficha["uf"])
                    if ficha.get("municipio"):
                        cod["rcboMunicipio"] = combos.municipio(s, ficha["uf"], ficha["municipio"])
                        if ficha.get("bairro") and cod.get("rcboMunicipio"):
                            cod["rcboBairro"] = combos.bairro(s, cod["rcboMunicipio"], ficha["bairro"])
            except Exception as e:
                print("  (!) resolvedor de combo falhou:", e)
            print("  códigos internos resolvidos:", {k: v for k, v in cod.items() if v})

        # --resolver-so: grava os códigos na ficha e SAI (sessão da descoberta). Depois encerra-se
        # a sessão e o tiro único roda num login novo, já com os códigos.
        if "--resolver-so" in sys.argv:
            ficha["codigosInternos"] = {k: v for k, v in cod.items() if v}
            io_ficha = args[0]
            import json as _j
            open(io_ficha, "w", encoding="utf-8").write(_j.dumps(ficha, ensure_ascii=False, indent=2))
            print(f"\n  códigos gravados na ficha ({io_ficha}). Encerre a sessão e rode o tiro único.")
            return

        def postar(rotulo: str, alvo: str, mudancas: dict):
            corpo = dict(estado)
            corpo.update(mudancas)
            corpo["__EVENTTARGET"] = alvo
            corpo["__EVENTARGUMENT"] = ""
            corpo["__ASYNCPOST"] = "true"
            corpo[n_sm] = f"{n_sm}|{alvo}"
            if id_ajax:
                corpo["RadAJAXControlID"] = id_ajax
            LIBERADOS.update(set(mudancas) | {"__EVENTTARGET", "__ASYNCPOST", n_sm, "RadAJAXControlID"})
            LIBERADOS.update(k for k, v in corpo.items() if estado.get(k) == v)
            if "rbSalvar" in alvo:  # o corpo exato da gravação, para confrontar com um que gravou
                (CAP / "direto_post_final.json").write_text(
                    json.dumps(corpo, ensure_ascii=False, indent=1), encoding="utf-8")
            resp = cronometrar(rotulo, lambda: s.post(url, corpo, ajax=True, headers=CAB))
            estado.update(mudancas)
            if not aplicar_delta(estado, resp.text):
                print("     (!) o delta não renovou o estado")
            return resp

        # ---- 2. os campos com AutoPostBack, um a um.
        # Com `--sem-postback` eles NÃO vão ao servidor um a um: ficam guardados e entram
        # todos no POST final. É o experimento que mede se os postbacks são mesmo necessários
        # (nunca testado com o endereço preenchido — antes falhava por falta dele).
        pendentes_sem_pb: dict[str, str] = {}
        if sem_postback:
            print("\n  --- SEM AutoPostBack: tudo junto no POST final ---")
            for sufixo, chave, indice in AUTOPOSTBACK:
                if chave not in ficha:
                    continue
                n = nome_longo(html, sufixo)
                if not n:
                    continue
                pendentes_sem_pb[n] = str(ficha[chave])
                # combo tem ClientState com o código resolvido; a data tem o sub-campo dateInput
                if sufixo.startswith(("rcbo", "rcb", "radCombo")):
                    csn = nome_client_state(html, sufixo)
                    if csn:
                        codigo = cod.get(sufixo) or codigo_do_texto(str(ficha[chave]))
                        pendentes_sem_pb[csn] = patch_cs(estado.get(csn, ""), str(ficha[chave]),
                                                         tipo="combo", codigo=codigo)
                if sufixo == "rdpDataNascimento":
                    pp = str(ficha[chave]).split("-")
                    if len(pp) == 3:
                        pendentes_sem_pb[f"{n}$dateInput"] = f"{pp[2]}/{pp[1]}/{pp[0]}"
            for sufixo, chave in AUTOPOSTBACK_ENDERECO:
                if chave not in ficha:
                    continue
                n = nome_longo(html, sufixo, dentro="UCEndereco")
                if not n:
                    continue
                pendentes_sem_pb[n] = str(ficha[chave])
                csn = nome_client_state(html, sufixo, dentro="UCEndereco")
                if csn:
                    codigo = cod.get(sufixo) or codigo_do_texto(str(ficha[chave]))
                    pendentes_sem_pb[csn] = patch_cs(estado.get(csn, ""), str(ficha[chave]),
                                                     tipo="combo", codigo=codigo)
            print(f"     {len(pendentes_sem_pb)} campos que normalmente teriam postback próprio")
        else:
            print("\n  --- AutoPostBacks ---")

        for sufixo, chave, indice in ([] if sem_postback else AUTOPOSTBACK):
            if chave not in ficha:
                continue
            n = nome_longo(html, sufixo)
            if not n:
                print(f"     (!) {sufixo} não existe nesta tela — pulado")
                continue
            alvo = f"{n}${indice}" if indice is not None else n
            mud = {n: str(ficha[chave])}
            if sufixo.startswith(("rcbo", "rcb", "radCombo")):
                cs = nome_client_state(html, sufixo)
                if cs:
                    codigo = cod.get(sufixo) or codigo_do_texto(str(ficha[chave]))
                    mud[cs] = patch_cs(estado.get(cs, ""), str(ficha[chave]), tipo="combo", codigo=codigo)
            # A data precisa do sub-campo dateInput em dd/mm/aaaa (o picker valida por ele).
            if sufixo == "rdpDataNascimento":
                p_ = str(ficha[chave]).split("-")
                if len(p_) == 3:
                    mud[f"{n}$dateInput"] = f"{p_[2]}/{p_[1]}/{p_[0]}"
            postar(f"   {sufixo[:26]}", alvo, mud)

        for sufixo, chave in ([] if sem_postback else AUTOPOSTBACK_ENDERECO):
            if chave not in ficha:
                continue
            n = nome_longo(html, sufixo, dentro="UCEndereco")
            if n:
                mud = {n: str(ficha[chave])}
                cs = nome_client_state(html, sufixo, dentro="UCEndereco")
                if cs:
                    codigo = cod.get(sufixo) or codigo_do_texto(str(ficha[chave]))
                    mud[cs] = patch_cs(estado.get(cs, ""), str(ficha[chave]), tipo="combo", codigo=codigo)
                postar(f"   UCEndereco.{sufixo[:18]}", n, mud)
            else:
                print(f"     (!) UCEndereco${sufixo} não encontrado")

        # ---- 3. os campos simples
        simples = {}
        for sufixo, chave in SIMPLES:
            if chave not in ficha:
                continue
            n = nome_longo(html, sufixo)
            if n:
                simples[n] = str(ficha[chave])
            else:
                print(f"     (!) {sufixo} não existe nesta tela")

        # ENDEREÇO — é a causa raiz do cadastro não gravar (ver APRENDIZADOS §12). Mira no
        # `$UCEndereco$` porque a tela tem TRÊS blocos de endereço com os mesmos sufixos.
        for sufixo, chave in ENDERECO:
            if chave not in ficha:
                continue
            n = nome_longo(html, sufixo, dentro="UCEndereco")
            if n:
                simples[n] = str(ficha[chave])
            else:
                print(f"     (!) UCEndereco${sufixo} não encontrado")

        # Os combos do POST final tambem precisam do ClientState (rcboTipo, rcbLogradouro,
        # rcboBairro, radComboBoxPais). Sem ele o servidor ve o campo como nao-selecionado.
        for sufixo, chave in ENDERECO + [("radComboBoxPais", "pais")]:
            if chave not in ficha or not str(ficha[chave]).strip():
                continue
            if not sufixo.startswith(("rcbo", "rcb", "radCombo")):
                continue
            dentro = None if sufixo == "radComboBoxPais" else "UCEndereco"
            cs = nome_client_state(html, sufixo, dentro=dentro)
            if cs:
                codigo = cod.get(sufixo) or codigo_do_texto(str(ficha[chave]))
                simples[cs] = patch_cs(estado.get(cs, ""), str(ficha[chave]), tipo="combo", codigo=codigo)

        # ClientState dos RadTextBox/masked: o servidor le' o valor DAQUI, nao do campo plano
        # (APRENDIZADOS §13). Reaproveita o schema que a tela mandou.
        for suf, chave, dentro in (("RadTextBoxCNS", "cns", None), ("txtCPF", "cpf", None),
                                    ("txtCEP", "cep", "UCEndereco")):
            if chave not in ficha or not str(ficha[chave]).strip():
                continue
            cs = nome_client_state(html, suf, dentro=dentro)
            if cs:
                simples[cs] = patch_cs(estado.get(cs, ""), str(ficha[chave]), tipo="texto")

        # O sub-campo dateInput em dd/mm/aaaa tambem no POST final.
        if "dataNascimento" in ficha:
            nd = nome_longo(html, "rdpDataNascimento")
            pp = str(ficha["dataNascimento"]).split("-")
            if nd and len(pp) == 3:
                simples[f"{nd}$dateInput"] = f"{pp[2]}/{pp[1]}/{pp[0]}"

        # ClientState da data de nascimento (RadDatePicker -> dateInput).
        if "dataNascimento" in ficha:
            csd = cs_data_nascimento(html)
            if csd:
                simples[csd] = patch_cs(estado.get(csd, ""), str(ficha["dataNascimento"]), tipo="data")

        # Seletor de origem por campo: o POST que gravou mandava os sete em "Prime".
        for g in GRUPOS_ORIGEM:
            n = nome_longo(html, g)
            if n:
                simples[n] = "radio" + g.split("_")[0][5:] + "Prime"

        simples.update(pendentes_sem_pb)
        print(f"\n  campos simples preenchidos: {len(simples)}")
        for k, v in simples.items():
            print(f"     {curto(k):<24} {v[:44]}")

        if not salvar:
            # Monta o corpo do Salvar e o grava em disco SEM enviar. É o que permite comparar
            # com um POST que gravou, sem tocar no sistema observado.
            alvo = alvo_postback(html, "rbSalvar") or "(alvo não encontrado)"
            corpo = dict(estado)
            corpo.update(simples)
            corpo["__EVENTTARGET"] = alvo
            corpo["__EVENTARGUMENT"] = ""
            corpo["__ASYNCPOST"] = "true"
            corpo[n_sm] = f"{n_sm}|{alvo}"
            if id_ajax:
                corpo["RadAJAXControlID"] = id_ajax
            (CAP / "direto_post_final.json").write_text(
                json.dumps(corpo, ensure_ascii=False, indent=1), encoding="utf-8")
            print(f"\n  PAROU ANTES DE SALVAR. Corpo do Salvar gravado em "
                  f"capturas/direto_post_final.json ({len(corpo)} campos), NÃO enviado.")
        else:
            alvo = alvo_postback(html, "rbSalvar")
            if not alvo:
                raise SystemExit("ABORTADO: alvo do rbSalvar não encontrado — postar vazio "
                                 "devolve 200 sem gravar (falso sucesso).")
            print(f"\n  alvo da gravação: {alvo}")
            resp = postar("   rbSalvar (GRAVA)", alvo, simples)
            (CAP / "direto_salvo.html").write_text(resp.text, encoding="utf-8")
            ids = re.findall(r'hidPacienteId[^>]{0,90}?value="?([0-9a-f-]{36})', resp.text)
            print("\n  pacienteId criado:", ids[0] if ids else "(não achado)")
            if not ids:
                erros = sorted(set(re.findall(r"Informe [^\"'<]{4,46}|Campo [^\"'<]{4,46}obrigat\w*", resp.text)))
                print("  mensagens na resposta:", " | ".join(erros[:8])[:260] or "(nenhuma)")

        print(f"\n  ---- custo ----  {len(tempos)} requisições, "
              f"{sum(t for _, t, _ in tempos)*1000:.0f} ms, "
              f"{sum(n for _, _, n in tempos):,} chars   total {time.perf_counter()-t0:.1f} s")


if __name__ == "__main__":
    main()
