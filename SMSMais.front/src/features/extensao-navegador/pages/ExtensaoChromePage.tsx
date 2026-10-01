import { useState } from 'react';
import { Download, Loader2, Puzzle, Settings2 } from 'lucide-react';
import { Link } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { useTemConsulta } from '@/shared/auth/authStore';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import { baixarInstalador, mensagemErroDoDownload } from '@/features/extensao-navegador/api/extensaoApi';
import { useSituacaoDistribuicao } from '@/features/extensao-navegador/api/queries';

/**
 * A porta de entrada da extensão para qualquer pessoa logada: baixar o instalador (que já sai
 * autorizado no nome de quem baixou) e os três passos para pô-la no Chrome.
 */
export function ExtensaoChromePage() {
  const situacao = useSituacaoDistribuicao();
  const podeGerenciar = useTemConsulta('ExtensaoNavegador');
  const [baixando, setBaixando] = useState(false);
  const [baixou, setBaixou] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const versao = situacao.data?.versaoAtualizador ?? null;
  const semInstalador = situacao.isSuccess && !versao;

  async function baixar() {
    setErro(null);
    setBaixando(true);
    try {
      await baixarInstalador();
      setBaixou(true);
    } catch (e) {
      setErro(await mensagemErroDoDownload(e));
    } finally {
      setBaixando(false);
    }
  }

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-start gap-3">
          <Puzzle className="mt-1 h-6 w-6 text-primary-600" />
          <div>
            <div className="flex items-center gap-1.5">
              <h1 className="text-2xl font-semibold text-gray-900">Extensão Chrome</h1>
              <AjudaManual artigo="extensao-chrome" />
            </div>
            <p className="mt-1 text-sm text-gray-600">
              Instale neste computador o programa que baixa a extensão e a mantém atualizada sozinho.
            </p>
          </div>
        </div>
        {podeGerenciar ? (
          <Link to="/app/extensao/gerenciar" className="btn btn-secondary shrink-0">
            <Settings2 className="h-4 w-4" />
            Computadores e versões
          </Link>
        ) : null}
      </header>

      {situacao.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(situacao.error)}
        </div>
      ) : null}

      <section className="max-w-3xl rounded-lg border border-gray-200 bg-white p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-base font-semibold text-gray-900">Instalar neste computador</h2>
            <p className="mt-0.5 text-sm text-gray-600">
              {semInstalador
                ? 'O instalador ainda não foi publicado nesta plataforma.'
                : versao
                  ? `Instalador v${versao}${situacao.data?.versaoExtensao ? ` · extensão v${situacao.data.versaoExtensao}` : ''}`
                  : ' '}
            </p>
          </div>
          <Button onClick={baixar} disabled={baixando || !versao}>
            {baixando ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
            Baixar instalador
          </Button>
        </div>

        {erro ? (
          <div className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}
        {baixou && !erro ? (
          <div className="mt-3 rounded-md border border-primary-200 bg-primary-50 px-3 py-2 text-sm text-primary-800">
            Instalador baixado. Ele vale por 1 hora e para <strong>um computador só</strong> — para instalar em
            outro, baixe de novo lá.
          </div>
        ) : null}

        <ol className="mt-5 space-y-3 text-sm text-gray-700">
          <li className="flex gap-3">
            <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary-100 text-xs font-semibold text-primary-700">
              1
            </span>
            <span>
              <strong>Execute o instalador.</strong> Não pede senha de administrador. Se o Windows avisar que
              "protegeu o computador", clique em <em>Mais informações</em> e em <em>Executar assim mesmo</em>.
            </span>
          </li>
          <li className="flex gap-3">
            <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary-100 text-xs font-semibold text-primary-700">
              2
            </span>
            <span>
              Ele se instala, <strong>autoriza este computador no seu nome</strong> e baixa a extensão. Um ícone "+"
              aparece perto do relógio.
            </span>
          </li>
          <li className="flex gap-3">
            <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-primary-100 text-xs font-semibold text-primary-700">
              3
            </span>
            <span>
              <strong>Só na primeira vez, no Chrome:</strong> abra <code className="rounded bg-gray-100 px-1">chrome://extensions</code>,
              ligue o <em>Modo do desenvolvedor</em>, clique em <em>Carregar sem compactação</em> e escolha a pasta
              que o instalador mostrar.
            </span>
          </li>
        </ol>
        <p className="mt-4 text-sm text-gray-600">
          Daí em diante a extensão se atualiza e se recarrega sozinha. O <em>Modo do desenvolvedor</em> precisa
          continuar ligado: sem ele o Chrome desativa a extensão.
        </p>
      </section>
    </div>
  );
}
