"""Reconstrói o inventário de scripts dos laboratórios a partir dos docstrings.

O `INDICE.md` ao lado é escrito à mão — agrupado por finalidade, com o que cada sonda já
respondeu, que é o que evita refazer experimento. Isso um gerador não sabe fazer.

O que ele sabe fazer, e é o que mata a validade do índice: dizer **o que existe hoje** e,
principalmente, **o que existe e não está no índice**. Rode depois de criar script novo:

    python catalogar.py            # inventário completo
    python catalogar.py --faltando # só o que o INDICE.md não menciona

Também marca o que está FORA do git — em 08/09/2026 eram 24 scripts que existiam só numa
máquina, o laboratório SERNIT inteiro entre eles.
"""
import ast
import pathlib
import subprocess
import sys

RAIZ = pathlib.Path(__file__).resolve().parents[2]
INDICE = pathlib.Path(__file__).resolve().parent.parent / "INDICE.md"
PASTAS = ["Automais.SISREG", "Automais.SER", "Automais.SERNIT", "Automais.SISCAN", "Salux/scripts"]


def rastreados() -> set[str]:
    saida = subprocess.run(
        ["git", "ls-files"], cwd=RAIZ, capture_output=True, text=True, encoding="utf-8"
    ).stdout
    return set(saida.splitlines())


def resumo(caminho: pathlib.Path) -> str:
    try:
        doc = ast.get_docstring(ast.parse(caminho.read_text(encoding="utf-8", errors="replace")))
    except Exception:
        return "(não parseou)"
    if not doc:
        return "(sem docstring)"
    linha = " ".join(doc.strip().splitlines()[0:2]).strip()
    return (linha[:150] + "…") if len(linha) > 150 else linha


def main(argv: list[str]) -> int:
    so_faltando = "--faltando" in argv
    texto_indice = INDICE.read_text(encoding="utf-8") if INDICE.exists() else ""
    conhecidos = rastreados()

    faltando = 0
    for pasta in PASTAS:
        base = RAIZ / pasta
        if not base.exists():
            continue

        arquivos = sorted(base.glob("*.py"))
        cabecalho_impresso = False

        for f in arquivos:
            no_indice = f.name in texto_indice
            if so_faltando and no_indice:
                continue

            if not cabecalho_impresso:
                print(f"\n########## {pasta}  ({len(arquivos)} scripts)")
                cabecalho_impresso = True

            marcas = []
            if f.relative_to(RAIZ).as_posix() not in conhecidos:
                marcas.append("FORA DO GIT")
            if not no_indice:
                marcas.append("AUSENTE DO ÍNDICE")
                faltando += 1

            sufixo = f"  [{' | '.join(marcas)}]" if marcas else ""
            print(f"- {f.name}{sufixo} :: {resumo(f)}")

    if so_faltando and faltando == 0:
        print("Índice em dia: todo script dos laboratórios está mencionado.")
    elif faltando:
        print(f"\n>>> {faltando} script(s) fora do INDICE.md. Acrescente uma linha para cada.")

    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
