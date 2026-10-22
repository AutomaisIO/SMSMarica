import { type ReactNode, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, Globe, History, Loader2, Smartphone } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { formatarInstante } from '@/shared/lib/datas';
import { AjudaManual } from '@/shared/ui/AjudaManual';
import { Button } from '@/shared/ui/Button';
import {
  type CanalEntrada,
  useConfiguracaoAppCidadao,
  useSalvarConfiguracaoAppCidadao,
} from '@/features/app-cidadao/api';

/**
 * Sistema → App do cidadão (ADR-0078). Como o paciente entra no PWA e no app nativo, cada canal com a
 * sua configuração. Só admin global: o servidor confere e responde 403 para os demais, e o menu nem
 * mostra o item. Cada ajuste aparece no histórico, com quem fez e o que mudou.
 */
export function AppCidadaoPage() {
  const configuracao = useConfiguracaoAppCidadao();
  const salvar = useSalvarConfiguracaoAppCidadao();
  const [pwa, setPwa] = useState<CanalEntrada>({ consultarEsus: false });
  const [nativo, setNativo] = useState<CanalEntrada>({ consultarEsus: false });
  const [salvo, setSalvo] = useState(false);

  useEffect(() => {
    if (!configuracao.data) return;
    setPwa(configuracao.data.pwa);
    setNativo(configuracao.data.nativo);
  }, [configuracao.data]);

  const dados = configuracao.data;
  const mudou =
    !!dados &&
    (dados.pwa.consultarEsus !== pwa.consultarEsus || dados.nativo.consultarEsus !== nativo.consultarEsus);

  function aoSalvar() {
    setSalvo(false);
    salvar.mutate({ pwa, nativo }, { onSuccess: () => setSalvo(true) });
  }

  return (
    <div className="space-y-8">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Smartphone className="h-6 w-6 text-primary-600" />
          App do cidadão
          <AjudaManual artigo="app-cidadao" />
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Como o paciente entra no aplicativo. O PWA (pelo navegador) e o app instalado no celular têm cada um a sua
          configuração. Só administradores com acesso global veem esta tela.
        </p>
      </header>

      {configuracao.isLoading ? (
        <div className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
        </div>
      ) : null}
      {configuracao.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(configuracao.error)}
        </div>
      ) : null}

      {dados ? (
        <>
          <section className="space-y-3">
            <h2 className="text-sm font-semibold uppercase tracking-wide text-gray-500">Entrada no app</h2>
            <p className="text-sm text-gray-600">
              O paciente digita o CPF e a data de nascimento e escolhe, entre os celulares do cadastro (só o final de
              cada número aparece), para qual o código vai pelo WhatsApp. Quando nenhum é dele, o app pode procurar
              outros números no e-SUS; sem nenhum, ele é orientado a procurar o posto.
            </p>
            {!dados.esusConfigurado ? (
              <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>
                  A conta da plataforma no e-SUS PEC não está gravada (ou está desligada) em{' '}
                  <Link to="/app/fontes-dados" className="font-medium underline">
                    Sistema → Fontes de dados
                  </Link>
                  . Enquanto isso, ligar a consulta não tem efeito: o app não oferece a busca.
                </span>
              </div>
            ) : null}

            <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
              <CartaoCanal
                icone={<Globe className="h-5 w-5 text-primary-600" />}
                titulo="PWA (navegador)"
                descricao="O app aberto pelo navegador, inclusive com o atalho na tela inicial do celular."
                valor={pwa}
                aoMudar={setPwa}
              />
              <CartaoCanal
                icone={<Smartphone className="h-5 w-5 text-primary-600" />}
                titulo="App nativo (Android e iPhone)"
                descricao="O aplicativo instalado pela loja."
                valor={nativo}
                aoMudar={setNativo}
              />
            </div>

            <div className="flex flex-wrap items-center gap-3">
              <Button onClick={aoSalvar} disabled={!mudou || salvar.isPending}>
                {salvar.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
                Salvar
              </Button>
              {salvo && !mudou ? <span className="text-sm text-green-700">Salvo.</span> : null}
              {salvar.isError ? (
                <span className="text-sm text-red-700">{extrairMensagemDeErro(salvar.error)}</span>
              ) : null}
            </div>
          </section>

          <section className="space-y-3">
            <h2 className="flex items-center gap-2 text-sm font-semibold uppercase tracking-wide text-gray-500">
              <History className="h-4 w-4" /> Histórico de ajustes
            </h2>
            {dados.ajustes.length === 0 ? (
              <p className="text-sm text-gray-500">Nenhum ajuste ainda: vale a configuração de fábrica (tudo desligado).</p>
            ) : (
              <ul className="divide-y divide-gray-100 rounded-md border border-gray-200 bg-white">
                {dados.ajustes.map((a, i) => (
                  <li key={`${a.em}-${i}`} className="space-y-0.5 px-3 py-2 text-sm">
                    <div className="text-xs text-gray-500">
                      {formatarInstante(a.em)}
                      {a.quem ? ` · por ${a.quem}` : ''}
                    </div>
                    {a.mudancas.map((m) => (
                      <p key={m} className="text-gray-800">
                        {m}
                      </p>
                    ))}
                  </li>
                ))}
              </ul>
            )}
          </section>
        </>
      ) : null}
    </div>
  );
}

function CartaoCanal({
  icone,
  titulo,
  descricao,
  valor,
  aoMudar,
}: {
  icone: ReactNode;
  titulo: string;
  descricao: string;
  valor: CanalEntrada;
  aoMudar: (v: CanalEntrada) => void;
}) {
  return (
    <div className="space-y-3 rounded-lg border border-gray-200 bg-white p-4">
      <div className="flex items-center gap-2">
        {icone}
        <h3 className="font-medium text-gray-900">{titulo}</h3>
      </div>
      <p className="text-sm text-gray-600">{descricao}</p>
      <label className="flex items-start gap-2 text-sm text-gray-800">
        <input
          type="checkbox"
          className="mt-0.5"
          checked={valor.consultarEsus}
          onChange={(e) => aoMudar({ ...valor, consultarEsus: e.target.checked })}
        />
        <span>
          <span className="font-medium">Consultar o e-SUS</span> quando nenhum número do cadastro for do paciente
          <span className="block text-xs text-gray-500">
            Usa a conta da plataforma, uma consulta por vez e sem derrubar ninguém. Pode levar alguns segundos.
          </span>
        </span>
      </label>
    </div>
  );
}
