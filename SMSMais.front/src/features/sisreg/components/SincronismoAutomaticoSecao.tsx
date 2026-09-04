import { useState } from 'react';
import { Loader2, Power, PowerOff, RefreshCwOff } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import {
  useAlternarSincronismoAutomatico,
  useConfiguracaoSisreg,
} from '@/features/sisreg/api/queries';

/**
 * Interruptor mestre do sincronismo automático com o SISREG.
 *
 * Fica no topo da tela e fora do formulário de credenciais porque é um botão de emergência: o
 * SISREG mantém UMA sessão por operador, então enquanto o sincronismo roda ele derruba (e é
 * derrubado por) qualquer outro uso da mesma credencial — recepção, laboratório de integração,
 * diagnóstico. Precisa de um clique, não de submeter o formulário inteiro.
 *
 * Não confundir com "Desabilitar todas" da seção de sincronismo logo abaixo: aquele desliga as ~45
 * agendas uma a uma e perde quem estava ligado; este só ignora o agendamento e devolve tudo como
 * estava ao religar.
 */
export function SincronismoAutomaticoSecao() {
  const config = useConfiguracaoSisreg();
  const alternar = useAlternarSincronismoAutomatico();
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const ligado = config.data?.sincronismoAutomaticoAtivo ?? true;
  const carregando = config.isPending || alternar.isPending;

  function aoAlternar(ativo: boolean) {
    setErro(null);
    setAviso(null);
    alternar.mutate(ativo, {
      onSuccess: (r) => setAviso(r.mensagem),
      onError: (e) => setErro(extrairMensagemDeErro(e)),
    });
  }

  return (
    <section
      className={`rounded-xl border p-5 shadow-sm ${
        ligado ? 'border-gray-200 bg-white' : 'border-amber-300 bg-amber-50'
      }`}
    >
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
            <RefreshCwOff className={`h-4 w-4 ${ligado ? 'text-primary-600' : 'text-amber-600'}`} />
            Sincronismo automático com o SISREG
            <span
              className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                ligado ? 'bg-green-100 text-green-700' : 'bg-amber-200 text-amber-900'
              }`}
            >
              {config.isPending ? '…' : ligado ? 'Ligado' : 'Desligado'}
            </span>
          </h2>
          <p className="mt-1 max-w-3xl text-xs text-gray-600">
            Chave mestra do que roda sozinho: a importação diária de cada unidade e a sincronização
            de profissionais e procedimentos. Desligar <strong>não apaga a programação</strong> de
            nenhuma unidade — religar devolve exatamente o horário que já estava configurado. Ações
            manuais desta tela continuam funcionando.
          </p>
          <p className="mt-1 max-w-3xl text-xs text-gray-500">
            O SISREG aceita uma sessão por operador: enquanto o sincronismo roda, ele derruba quem
            estiver usando a mesma credencial (e vice-versa). Desligue antes de trabalhar direto no
            SISREG com o mesmo login.
          </p>
        </div>

        <div className="flex shrink-0 items-center gap-2">
          {carregando ? <Loader2 className="h-4 w-4 animate-spin text-gray-400" /> : null}
          {ligado ? (
            <Button
              variante="outline"
              tamanho="sm"
              disabled={carregando}
              onClick={() => aoAlternar(false)}
            >
              <PowerOff className="mr-1.5 h-3.5 w-3.5" />
              Desligar sincronismo
            </Button>
          ) : (
            <Button tamanho="sm" disabled={carregando} onClick={() => aoAlternar(true)}>
              <Power className="mr-1.5 h-3.5 w-3.5" />
              Religar sincronismo
            </Button>
          )}
        </div>
      </div>

      {/* Enquanto está desligado o aviso fica de pé mesmo depois de recarregar a página: é o
          estado que se esquece ligado e vira "por que parou de importar?" semanas depois. */}
      {!ligado && !aviso ? (
        <p className="mt-3 rounded-md border border-amber-200 bg-white px-3 py-2 text-xs text-amber-800">
          Nenhuma unidade está importando sozinha. Enquanto estiver assim, agendamentos novos do
          SISREG só entram por ação manual.
        </p>
      ) : null}

      {aviso ? (
        <p className="mt-3 rounded-md border border-gray-200 bg-white px-3 py-2 text-xs text-gray-700">
          {aviso}
        </p>
      ) : null}

      {erro ? (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">
          {erro}
        </p>
      ) : null}
    </section>
  );
}
