"""Cadastro de paciente pelo caminho DIRETO, cronometrado. Por padrão PARA ANTES DE SALVAR.

O caminho que a tela faz, sem passear por ela:
  1. GET  AgendaRecepcao.aspx           — pega o estado do servidor (EV/VIEWSTATE)
  2. POST lupa (imbConsulta)            — busca o CPF; a grade diz se existe no Prime
  3. POST imbCadastro da linha          — o servidor monta o cross-page post com os `key_*`
  4. POST do form intermediário         — cai em CadastroPaciente.aspx JÁ PREENCHIDO pelo CadWeb
  5. POST rbSalvar                      — grava (só com --salvar)

Medido em 21/09/2026: a coluna "Código" da grade distingue paciente do Prime (GUID real) de eco
do CadWeb (`00000000-...`). É esse o teste de existência — não "a busca retornou linha".

Uso:  python probe_cadastro.py <CPF> [--unidade GUID] [--curto] [--salvar]

`--curto` reaproveita o HTML da agenda já baixado em vez de refazer o GET (o passo caro).
É o modo que uma carga em massa usaria: 1 GET para N pacientes.
"""

from __future__ import annotations

import os
import re
import sys
import time

from prime.client import (APP, CAP, LIBERADOS, PrimeSession, action_do_form, campos_todos, sopa)

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

URL_AGENDA = f"{APP}/Agendamento/AgendaRecepcao.aspx"
GUID_NULO = "00000000-0000-0000-0000-000000000000"
tempos: list[tuple[str, float, int]] = []


def curto(n: str) -> str:
    return n.rsplit("$", 1)[-1]


def nome_longo(html: str, sufixo: str) -> str | None:
    for m in re.finditer(r'name="([^"]+)"', html):
        if curto(m.group(1)) == sufixo:
            return m.group(1)
    return None


def alvo_postback(html: str, sufixo: str) -> str | None:
    """Nome do controle para o `__EVENTTARGET`, para RadButton do Telerik.

    Medido 21/09/2026: o RadButton NÃO tem `name` com o sufixo do controle — ele renderiza
    `<input name="…$rbSalvar_input">` e o id `…_rbSalvar`. Procurar por `name` devolve None,
    e um POST com `__EVENTTARGET` vazio volta 200 **sem gravar nada** (a página só recarrega).
    O alvo verdadeiro está no `_postBackReference` que o botão publica no JS.
    """
    for m in re.finditer(r"WebForm_PostBackOptions\('([^']+)'", html):
        if curto(m.group(1)) == sufixo:
            return m.group(1)
    for m in re.finditer(r"__doPostBack\('([^']+)'", html):
        if curto(m.group(1)) == sufixo:
            return m.group(1)
    return None


def liberar_inalterados(dados: dict, originais: dict) -> None:
    """A trava barra por NOME; liberamos só o que devolvemos idêntico ao que a tela mandou.
    O que eu ALTERAR ou ACRESCENTAR continua barrado — inclusive os botões de gravação."""
    LIBERADOS.update(k for k, v in dados.items() if k in originais and originais[k] == v)


def blocos_delta(texto: str):
    """Fatia a resposta do ASP.NET AJAX: `len|tipo|id|conteudo|`, repetido.

    O `len` é obrigatório para fatiar certo — o conteúdo contém `|` à vontade, então split('|')
    corrompe o valor.
    """
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
        tipo, ident = texto[j + 1:k], texto[k + 1:m2]
        conteudo = texto[m2 + 1:m2 + 1 + n]
        yield tipo, ident, conteudo
        i = m2 + 1 + n + 1


def aplicar_delta(campos: dict, resposta: str) -> int:
    """Atualiza VIEWSTATE/EVENTVALIDATION etc. a partir do delta.

    Sem isto, o postback seguinte vai com o `__EVENTVALIDATION` velho. Cada AutoPostBack
    renova o estado, e usar o anterior faz o servidor ignorar o evento em silêncio.
    """
    trocados = 0
    for tipo, ident, conteudo in blocos_delta(resposta):
        if tipo != "hiddenField":
            continue
        for k in list(campos):
            if k == ident or curto(k) == ident:
                campos[k] = conteudo
                trocados += 1
    return trocados


def cronometrar(rotulo, fn):
    t0 = time.perf_counter()
    r = fn()
    dt = time.perf_counter() - t0
    tempos.append((rotulo, dt, len(r.text)))
    print(f"  {rotulo:<34} {dt*1000:7.0f} ms   {len(r.text):>9,} chars   status={r.status_code}")
    return r


def main() -> None:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    salvar = "--salvar" in sys.argv
    curto_modo = "--curto" in sys.argv
    if "--unidade" in sys.argv:
        os.environ["PRIME_UNIDADE"] = sys.argv[sys.argv.index("--unidade") + 1]
    if not args:
        raise SystemExit(__doc__)
    cpf = re.sub(r"\D", "", args[0])

    with PrimeSession() as s:
        t_total = time.perf_counter()
        s.entrar()

        # ---- 1. estado do servidor
        # CAMINHO CURTO: o `__EVENTVALIDATION` do Prime não é nonce — o mesmo já serviu a três
        # POSTs distintos ao longo de 2,5 min (medido no acervo da extensão). Então, para uma
        # carga em massa, o GET da agenda (o passo caro: ~3 s e 464 KB) é UMA vez, não uma por
        # paciente. Aqui reaproveitamos o HTML já baixado.
        guardado = CAP / "agenda_recepcao.html"
        if curto_modo and guardado.exists():
            html = guardado.read_text(encoding="utf-8")
            print(f"  1. GET agenda                        REAPROVEITADO   {len(html):>9,} chars")
            s.ref = f"https://marica.ecosistemas.com.br{URL_AGENDA}"
        else:
            r = cronometrar("1. GET agenda", lambda: s.get(URL_AGENDA))
            html = r.text
            guardado.write_text(html, encoding="utf-8")

        # ---- 2. busca pelo CPF
        originais = campos_todos(html)
        d = dict(originais)
        d[nome_longo(html, "txtPaciente")] = cpf
        d[nome_longo(html, "rblPesquisarPor")] = "CPF"
        d["__EVENTTARGET"] = ""
        d["__EVENTARGUMENT"] = ""
        lupa = nome_longo(html, "imbConsulta")
        d[f"{lupa}.x"], d[f"{lupa}.y"] = "10", "10"
        liberar_inalterados(d, originais)
        r = cronometrar("2. POST busca (lupa)", lambda: s.post(action_do_form(html, URL_AGENDA), d))
        h2 = r.text
        (CAP / "cad_busca.html").write_text(h2, encoding="utf-8")

        # ---- a grade: existe no Prime ou é eco do CadWeb?
        g = sopa(h2).find(id=re.compile(r"gridBuscaPaciente$"))
        linha_codigo, nome_paciente = None, None
        if g:
            for tr in g.find_all("tr"):
                cels = [c.get_text(" ", strip=True) for c in tr.find_all("td")]
                if len(cels) > 3 and re.fullmatch(r"[0-9a-f-]{36}", cels[1] or ""):
                    linha_codigo, nome_paciente = cels[1], cels[3]
                    break
        print(f"\n  paciente: {nome_paciente}")
        print(f"  Código:   {linha_codigo}")
        if linha_codigo and linha_codigo != GUID_NULO:
            print("  >>> JÁ EXISTE no Prime — cadastro desnecessário.")
            return
        print("  >>> não existe no Prime (eco do CadWeb) — segue para o cadastro.\n")

        # ---- 3. a setinha da linha: o servidor monta o cross-page post com os key_*
        imb = nome_longo(h2, "imbCadastro")
        if not imb:
            raise SystemExit("imbCadastro não encontrado na linha da grade")
        orig2 = campos_todos(h2)
        d2 = dict(orig2)
        d2["__EVENTTARGET"] = ""
        d2["__EVENTARGUMENT"] = ""
        d2[f"{imb}.x"], d2[f"{imb}.y"] = "8", "8"
        liberar_inalterados(d2, orig2)
        r = cronometrar("3. POST imbCadastro (linha)", lambda: s.post(action_do_form(h2, URL_AGENDA), d2))
        h3 = r.text
        (CAP / "cad_crosspage.html").write_text(h3, encoding="utf-8")

        # ---- 4. o form intermediário se auto-submete no navegador; aqui à mão
        chaves = {k: v for k, v in campos_todos(h3).items() if curto(k).startswith("key_")}
        print(f"\n  campos key_* recebidos do CadWeb: {len(chaves)}")
        for k, v in sorted(chaves.items()):
            print(f"     {curto(k):<34} {str(v)[:46]}")
        if not chaves:
            print("  (nenhum key_* — o cross-page não veio como esperado; ver capturas/cad_crosspage.html)")
            return

        alvo = action_do_form(h3, str(r.url))
        orig3 = campos_todos(h3)
        d3 = dict(orig3)
        liberar_inalterados(d3, orig3)
        r = cronometrar("4. POST -> CadastroPaciente", lambda: s.post(alvo, d3))
        h4 = r.text
        (CAP / "cad_form.html").write_text(h4, encoding="utf-8")
        print(f"     caiu em: {r.url}")

        # ---- o que a tela de cadastro trouxe preenchido, e o que falta
        d4 = campos_todos(h4)
        interesse = ["txtNome", "txtCPF", "RadTextBoxCNS", "txtNomeMae", "txtNomePai",
                     "rdpDataNascimento", "rblSexo", "txtCelular", "rcboRaca",
                     "rcboUfNascimento", "rcboMunicipioNascimento", "txtCEP", "rcbLogradouro",
                     "txtNumero", "rcboBairro", "rcboMunicipio", "hidEnderecoId"]
        print("\n  tela de cadastro — o que veio preenchido:")
        faltando = []
        for nome in interesse:
            achou = [v for k, v in d4.items() if curto(k) == nome and v not in ("", None)]
            valor = achou[0] if achou else ""
            if valor and valor.lower() not in ("selecione",):
                print(f"     {nome:<26} {str(valor)[:44]}")
            else:
                faltando.append(nome)
        print(f"\n  VAZIOS/pendentes: {', '.join(faltando) if faltando else '(nenhum)'}")

        # ---- o que o CadWeb não entrega e a tela exige. Medido 21/09/2026:
        #  * o CadWeb NUNCA manda nome do pai; o Prime tem checkbox próprio para isso
        #    (`chkNomePaiDesconhecido`), que é melhor do que escrever "DESCONHECIDO" no campo
        #    de texto — fica dado estruturado para quem revisar depois;
        #  * os dois `Deseja informar...?` são obrigatórios (asterisco) e perguntam CONSENTIMENTO,
        #    não o dado. "N" = não informar;
        #  * o tipo de logradouro o Prime NÃO traduz do CadWeb (ele traduz raça, mas não este):
        #    008 no CadWeb veio para duas AVENIDAS, e 008 no Prime é VILA. Não dá para confiar
        #    no código — o tipo sai do texto do logradouro.
        complementos: dict[str, str] = {}

        def por_sufixo(sufixo: str, valor: str) -> None:
            n = nome_longo(h4, sufixo)
            if n:
                complementos[n] = valor
            else:
                print(f"     (!) campo {sufixo} não encontrado na tela")

        # Obrigatórios do Prime (informados pelo operador, 21/09/2026): nacionalidade, raça/cor,
        # CNS, CPF, nome, nome da mãe, nome do pai, sexo e data de nascimento. Todos vêm do
        # CadWeb — MENOS o nome do pai, que ele nunca manda.
        if not [v for k, v in d4.items() if curto(k) == "txtNomePai" and v]:
            por_sufixo("chkNomePaiDesconhecido", "on")
        if not [v for k, v in d4.items() if curto(k) == "txtNomeMae" and v]:
            por_sufixo("chkNomeMaeDesconhecido", "on")
        # Obrigatórios que não são dado do paciente, e sim CONSENTIMENTO ("Deseja informar...?").
        por_sufixo("rblEstaSituacaoRua", "N")
        por_sufixo("rblIdentidadeGenero", "N")
        por_sufixo("rblOrientacao", "N")
        # UF de nascimento: derivada com certeza do município que o CadWeb mandou.
        por_sufixo("rcboUfNascimento", "RJ")
        # ENDEREÇO NÃO É OBRIGATÓRIO e não entra na carga: o que o CadWeb mandou (CEP, logradouro,
        # número, bairro, município) segue no POST porque a tela já o trouxe, mas NÃO completamos
        # o que veio vazio (tipo de logradouro, UF do endereço) — é a recepção que confere.
        # Isso também tira do caminho crítico o desalinhamento 008-CadWeb x 008-Prime.

        print("\n  complementos que eu acrescento:")
        for k, v in complementos.items():
            print(f"     {curto(k):<30} {v}")

        # O corpo exato que eu mandaria, para poder confrontá-lo com um POST que funcionou.
        import json as _json
        (CAP / "cad_post.json").write_text(
            _json.dumps({**d4, **complementos}, ensure_ascii=False, indent=1), encoding="utf-8")

        # ---- os complementos NÃO podem ir todos no POST final.
        # Medido 21/09/2026 observando a recepção pela extensão: cada um desses controles é
        # AutoPostBack — o operador clica, a tela vai ao servidor, e só no fim vem o Salvar:
        #   rcboUfNascimento -> rcboMunicipioNascimento -> rblOrientacao$1 ->
        #   rblIdentidadeGenero$1 -> rblEstaSituacaoRua$1 -> chkNomePaiDesconhecido -> rbSalvar
        # Mandar os seis de uma vez no POST do Salvar devolve 200 e NÃO grava: o servidor nunca
        # processou os eventos. O `$1` no __EVENTTARGET é o ÍNDICE do item no RadioButtonList;
        # o valor vai no campo sem índice.
        n_sm_seq = nome_longo(h4, "RadScriptManager1") or "ctl00$ctl00$RadScriptManager1"
        id_ajax_seq = next(iter(re.findall(r'id="([^"]*RadAjaxManager1)"', h4)),
                           "ctl00_ctl00_DefaultContent_ChildDefaultContent_RadAjaxManager1")
        CAB_AJAX = {"X-MicrosoftAjax": "Delta=true", "Cache-Control": "no-cache",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"}
        url_cad = action_do_form(h4, str(r.url))
        estado = dict(d4)

        def autopostback(rotulo: str, sufixo: str, valor: str, indice: str | None) -> None:
            n = nome_longo(h4, sufixo)
            if not n:
                print(f"     (!) {sufixo} não encontrado — pulado")
                return
            alvo = f"{n}${indice}" if indice is not None else n
            estado[n] = valor
            corpo = dict(estado)
            corpo["__EVENTTARGET"] = alvo
            corpo["__EVENTARGUMENT"] = ""
            corpo["__ASYNCPOST"] = "true"
            corpo[n_sm_seq] = f"{n_sm_seq}|{alvo}"
            corpo["RadAJAXControlID"] = id_ajax_seq
            LIBERADOS.update({n, "__EVENTTARGET", "__ASYNCPOST", n_sm_seq, "RadAJAXControlID"})
            liberar_inalterados(corpo, estado)
            resp = cronometrar(rotulo, lambda: s.post(url_cad, corpo, ajax=True, headers=CAB_AJAX))
            trocados = aplicar_delta(estado, resp.text)
            if not trocados:
                print(f"     (!) {sufixo}: o delta não renovou o estado — o próximo POST pode falhar")

        if salvar:
            print("\n  --- AutoPostBacks (a tela faz um por campo) ---")
            autopostback("a. rcboUfNascimento", "rcboUfNascimento", "RJ", None)
            municipio = next((v for k, v in d4.items()
                              if curto(k) == "rcboMunicipioNascimento" and v), None)
            if municipio:
                autopostback("b. rcboMunicipioNascimento", "rcboMunicipioNascimento", municipio, None)
            autopostback("c. rblOrientacao", "rblOrientacao", "N", "1")
            autopostback("d. rblIdentidadeGenero", "rblIdentidadeGenero", "N", "1")
            autopostback("e. rblEstaSituacaoRua", "rblEstaSituacaoRua", "N", "1")
            if not [v for k, v in d4.items() if curto(k) == "txtNomePai" and v]:
                autopostback("f. chkNomePaiDesconhecido", "chkNomePaiDesconhecido", "on", None)
            d4 = estado  # o Salvar parte do estado JÁ processado pelo servidor
            complementos = {}

        if not salvar:
            print("\n  PAROU ANTES DE SALVAR (rode com --salvar para gravar).")
        else:
            alvo5 = alvo_postback(h4, "rbSalvar")
            if not alvo5:
                raise SystemExit("ABORTADO: alvo do rbSalvar não encontrado. Postar com "
                                 "__EVENTTARGET vazio devolve 200 e NÃO grava — falso sucesso.")
            print(f"\n  alvo do postback de gravação: {alvo5}")
            # O Salvar da tela NÃO é postback normal: é partial postback do ASP.NET AJAX
            # (UpdatePanel/RadAjax). Medido 21/09/2026 comparando com um cadastro que funcionou —
            # sem estes três campos e sem o header `X-MicrosoftAjax: Delta=true`, o servidor
            # responde 200 com a página inteira e NÃO executa o handler. Falso sucesso.
            n_sm = nome_longo(h4, "RadScriptManager1") or "ctl00$ctl00$RadScriptManager1"
            id_ajax = next(iter(re.findall(r'id="([^"]*RadAjaxManager1)"', h4)),
                           "ctl00_ctl00_DefaultContent_ChildDefaultContent_RadAjaxManager1")
            d5 = dict(d4)
            d5.update(complementos)
            d5["__EVENTTARGET"] = alvo5
            d5["__EVENTARGUMENT"] = ""
            d5["__ASYNCPOST"] = "true"
            d5[n_sm] = f"{n_sm}|{alvo5}"
            d5["RadAJAXControlID"] = id_ajax
            LIBERADOS.update({"__ASYNCPOST", n_sm, "RadAJAXControlID"})
            # Gravação autorizada pelo operador para ESTE cadastro. Os complementos entram na
            # allowlist um a um, pelo nome — nada de afrouxar a trava no geral.
            LIBERADOS.add("__EVENTTARGET")
            LIBERADOS.update(complementos)
            liberar_inalterados(d5, d4)
            # O corpo EXATO do Salvar, para confrontar com um POST que gravou.
            import json as _json2
            (CAP / "cad_post_final.json").write_text(
                _json2.dumps(d5, ensure_ascii=False, indent=1), encoding="utf-8")
            alvo_url = action_do_form(h4, str(r.url))
            r = cronometrar("5. POST rbSalvar (GRAVA)", lambda: s.post(
                alvo_url, d5, ajax=True,
                headers={"X-MicrosoftAjax": "Delta=true", "Cache-Control": "no-cache",
                         "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"}))
            (CAP / "cad_salvo.html").write_text(r.text, encoding="utf-8")
            achado = re.search(r'hidPacienteId"[^>]{0,80}?value="([0-9a-f-]{36})"', r.text)
            print("\n  pacienteId criado:", achado.group(1) if achado else "(não achado — ver capturas/cad_salvo.html)")

        # ---- custo
        total = time.perf_counter() - t_total
        # O que importa para a carga em massa é o custo MARGINAL: o login e o GET da agenda
        # acontecem uma vez para N pacientes; o resto se repete por paciente.
        por_paciente = [t for rot, t, _ in tempos if not rot.startswith("1.")]
        print(f"\n  ---- custo ----")
        for rot, t, n in tempos:
            print(f"    {rot:<32} {t*1000:7.0f} ms   {n:>9,} chars")
        print(f"  requisições: {len(tempos)}   soma: {sum(t for _, t, _ in tempos)*1000:.0f} ms"
              f"   bytes: {sum(n for _, _, n in tempos):,} chars")
        print(f"  tempo total (com login/sessão): {total:.1f} s")
        print(f"  >>> POR PACIENTE (sem login e sem o GET inicial): "
              f"{len(por_paciente)} requisições, {sum(por_paciente)*1000:.0f} ms")


if __name__ == "__main__":
    main()
