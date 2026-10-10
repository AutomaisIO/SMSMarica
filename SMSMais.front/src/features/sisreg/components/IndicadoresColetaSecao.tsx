import { useState } from 'react';
import { Gauge, Loader2, PlayCircle, RotateCcw } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import {
  useRearmarColetaIndicadores,
  useRetomarColetaIndicadores,
  useSalvarColetaIndicadores,
  useStatusColetaIndicadores,
} from '@/features/sisreg/api/queries';
import type {
  ColetaIndicadoresStatus,
  ColetorIndicadorSisreg,
  EsperaColetaIndicadores,
  FalhaColetaIndicador,
} from '@/features/sisreg/types';

const NOME_COLETOR: Record<ColetorIndicadorSisreg, string> = {
  Faltas: 'Faltas (absenteísmo oficial)',
  FaltasRecentes: 'Faltas das últimas semanas (ficha do paciente)',
  Canceladas: 'Marcações canceladas do mês',
  Desfechos: 'Devolvidas, negadas e canceladas antes de agendar',
  Ppi: 'Cotas PPI',
};

const TEXTO_ESPERA: Record<EsperaColetaIndicadores, string> = {
  Desligada: 'Desligado.',
  ChaveMestraDesligada: 'Parado: o sincronismo automático do SISREG (no topo da tela) está desligado.',
  PausadaPorCaptcha: 'Pausado: o SISREG pediu CAPTCHA.',
  ForaDoHorario: 'Aguardando o horário (01:20 às 18:00): à noite a sessão é da varredura das agendas.',
  OutroMotorUsandoASessao: 'Aguardando a vez: outro motor está usando a sessão do SISREG.',
  TetoDoColetor: 'Aguardando: atingiu o teto do coletor nesta hora.',
  OrcamentoGlobalCurto: 'Aguardando: o orçamento de requisições do operador está apertado nesta hora.',
  LoginRecusado:
    'Aguardando: o SISREG recusou o login. O coletor tenta de novo sozinho em alguns minutos; se continuar, confira o Endereço do SISREG em Integrações.',
};

function dataHora(iso: string | null) {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', {
    timeZone: 'America/Sao_Paulo',
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function janela(inicio: string, fim: string) {
  const f = (d: string) => `${d.slice(8, 10)}/${d.slice(5, 7)}/${d.slice(0, 4)}`;
  return inicio === fim ? f(inicio) : `${f(inicio)} a ${f(fim)}`;
}

function descreverLeitura(f: FalhaColetaIndicador) {
  return (
    `${NOME_COLETOR[f.coletor]}, ${janela(f.inicio, f.fim)}`
    + (/^\d{7}$/.test(f.escopo) ? ` (unidade ${f.escopo})` : '')
    + (f.escopo === 'amostra' ? ' (amostra de motivos)' : '')
  );
}

/** Quando o que está na fila vai ser lido — a resposta que faltava depois de "Tentar todas de novo". */
function quandoVaiLer(s: ColetaIndicadoresStatus) {
  if (!s.ativa) return 'A coleta automática está desligada: ligue-a acima para que sejam lidas.';
  if (s.pausadaAte) return 'O coletor está pausado por CAPTCHA: só voltam a ser lidas depois de retomar.';
  if (!s.chaveMestraLigada) return 'O sincronismo automático do SISREG (topo da tela) está desligado: ligue-o para que sejam lidas.';
  if (s.espera === 'ForaDoHorario')
    return 'Serão lidas a partir das 01:20 — à noite a sessão do SISREG é da varredura das agendas.';
  if (s.espera === 'LoginRecusado')
    return 'Serão lidas quando o SISREG voltar a aceitar o login — o coletor tenta de novo sozinho em alguns minutos.';
  return 'Serão lidas nos próximos minutos, uma requisição a cada 30 segundos, intercaladas com os outros motores.';
}

/**
 * Coletor dos INDICADORES DE REGULAÇÃO: mantém em dia, a partir de agora, o que a tela Regulação →
 * SISREG → Indicadores mostra e o espelho da agenda não tem — lista oficial de faltas, cotas PPI,
 * marcações canceladas do mês e devolvidas/negadas por unidade.
 *
 * <p>Nasce desligado. Cada leitura sai do mesmo orçamento anti-robô do operador que o robô de produção
 * usa; por isso o coletor anda devagar (uma requisição a cada 30 segundos, no máximo 150 por hora),
 * só de dia e cedendo a vez a qualquer outro motor.</p>
 */
export function IndicadoresColetaSecao() {
  const status = useStatusColetaIndicadores();
  const salvar = useSalvarColetaIndicadores();
  const retomar = useRetomarColetaIndicadores();
  const rearmar = useRearmarColetaIndicadores();
  const [erro, setErro] = useState<string | null>(null);
  // Desfecho do "Tentar todas de novo" fica na seção até a pessoa fechar: sem isto a lista vermelha
  // simplesmente sumia e não havia como saber o que tinha acontecido.
  const [rearmadas, setRearmadas] = useState<number | null>(null);

  const s = status.data;
  const ocupado = salvar.isPending || retomar.isPending || rearmar.isPending;

  function acao(p: Promise<unknown>) {
    setErro(null);
    p.catch((e) => setErro(extrairMensagemDeErro(e)));
  }

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-4">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-gray-900">
          <Gauge className="h-4 w-4 text-primary-600" />
          Indicadores de Regulação — coleta no SISREG
        </h2>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          Mantém em dia o que a tela <strong>Regulação → SISREG → Indicadores</strong> precisa e a
          agenda espelhada não traz: a <strong>lista oficial de faltas</strong> (os agendamentos em
          que a unidade registrou falta; lida quando a semana já tem 30 dias, depois que as unidades
          terminaram de apontar), as <strong>cotas PPI</strong>{' '}
          do mês fechado, as <strong>marcações canceladas</strong> do mês e as solicitações{' '}
          <strong>devolvidas, negadas e canceladas antes de agendar</strong>, unidade por unidade. O
          passado (jan/2025 a ago/2026) já foi carregado; o coletor cuida só dos últimos meses.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-600">
          A mesma lista de faltas é lida também das <strong>semanas com menos de 30 dias</strong>, de
          hora em hora (rede inteira, cerca de 10 requisições por rodada). Essa leitura não entra no
          indicador — a unidade ainda está apontando — e serve à ficha do paciente, onde a falta já
          registrada aparece na hora e o agendamento sem apontamento aparece como “Em aberto”.
        </p>
        <p className="mt-1 max-w-3xl text-xs text-gray-500">
          Anda devagar de propósito: uma requisição a cada 30 segundos, no máximo 150 por hora, só
          entre 01:20 e 18:00, e espera sempre que outro motor está usando a sessão. Se o SISREG pedir
          CAPTCHA, para por um dia inteiro. Os motivos de cancelamento do dia a dia vêm da conciliação
          de cancelamentos, que já lê essa tela; os dos meses passados, de uma amostra de 6 páginas por mês
          (cerca de 120 requisições para jan/2025 a ago/2026), sem mudar o total oficial de cada mês.
        </p>
      </header>

      {s ? (
        <>
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={s.ativa}
              disabled={ocupado}
              onChange={(e) => acao(salvar.mutateAsync(e.target.checked))}
            />
            Coletar automaticamente
          </label>

          {s.pausadaAte ? (
            <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
              <p className="font-medium">
                Pausado até {dataHora(s.pausadaAte)}: o SISREG pediu CAPTCHA para o operador da integração.
              </p>
              <p className="mt-1">
                Relogar não resolve. Abra o SISREG no navegador com esse usuário, resolva o CAPTCHA e só
                então retome.
              </p>
              <Button
                variante="outline"
                className="mt-2"
                disabled={ocupado}
                onClick={() => acao(retomar.mutateAsync())}
              >
                <PlayCircle className="mr-2 h-4 w-4" />
                Já resolvi — retomar
              </Button>
            </div>
          ) : null}

          {s.ativa ? (
            <p className="mt-3 rounded-lg border border-gray-200 bg-gray-50 p-3 text-xs text-gray-700">
              {s.trabalhoAtual && !s.espera ? (
                <span className="flex items-center gap-2">
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  Lendo agora: {s.trabalhoAtual}.
                </span>
              ) : s.espera ? (
                <>
                  {TEXTO_ESPERA[s.espera]}
                  {s.trabalhoAtual ? ` Em curso: ${s.trabalhoAtual}.` : ''}
                </>
              ) : (
                'Em dia — nada para ler agora.'
              )}
              <span className="mt-1 block text-gray-500">
                {s.requisicoesNaUltimaHora} de {s.tetoPorHora} requisições na última hora · último passo{' '}
                {dataHora(s.ultimoPassoEm)}
              </span>
            </p>
          ) : null}

          <div className="mt-4 overflow-x-auto">
            <table className="min-w-full text-xs">
              <thead className="text-left text-gray-500">
                <tr>
                  <th className="py-1 pr-4 font-medium">O que é lido</th>
                  <th className="py-1 pr-3 text-right font-medium">Lidas</th>
                  <th className="py-1 pr-3 text-right font-medium">Na fila</th>
                  <th className="py-1 pr-3 text-right font-medium">Com falha</th>
                  <th className="py-1 font-medium">Última leitura</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 text-gray-700">
                {s.coletores.map((c) => (
                  <tr key={c.coletor}>
                    <td className="py-1.5 pr-4">{NOME_COLETOR[c.coletor]}</td>
                    <td className="py-1.5 pr-3 text-right">{c.concluidas}</td>
                    <td className="py-1.5 pr-3 text-right">{c.pendentes + c.emAndamento}</td>
                    <td className={`py-1.5 pr-3 text-right ${c.falhas ? 'font-medium text-red-700' : ''}`}>
                      {c.falhas}
                    </td>
                    <td className="py-1.5">{dataHora(c.ultimaLeituraEm)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {rearmadas !== null ? (
            <div className="mt-4 rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-xs text-emerald-800">
              <p className="font-medium">
                {rearmadas === 0
                  ? 'Nenhuma leitura estava em falha.'
                  : `${rearmadas} leitura${rearmadas === 1 ? '' : 's'} voltou${rearmadas === 1 ? '' : 'aram'} para a fila, com as 6 tentativas de novo.`}
              </p>
              {rearmadas > 0 ? <p className="mt-1">{quandoVaiLer(s)}</p> : null}
              <p className="mt-1 text-emerald-700">
                Acompanhe na tabela: saem de “Com falha”, ficam em “Na fila” e vão para “Lidas” quando fecham. Se
                falharem de novo, voltam para a lista vermelha.
              </p>
              <button
                type="button"
                className="mt-1 text-emerald-700 underline"
                onClick={() => setRearmadas(null)}
              >
                Fechar aviso
              </button>
            </div>
          ) : null}

          {s.ultimasFalhas.length > 0 ? (
            <div className="mt-4 rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-800">
              <p className="font-medium">Leituras que não fecharam — o período fica fora do indicador até fechar:</p>
              <ul className="mt-1 space-y-0.5">
                {s.ultimasFalhas.map((f) => (
                  <li key={`${f.coletor}-${f.inicio}-${f.escopo}`}>
                    {descreverLeitura(f)} — {f.erro ?? 'sem mensagem'} ({f.tentativas} tentativa
                    {f.tentativas === 1 ? '' : 's'})
                  </li>
                ))}
              </ul>
              <p className="mt-2 text-red-700">
                Falhas são tentadas de novo sozinhas no dia seguinte, até 6 vezes — as das últimas
                semanas, na rodada da hora seguinte. Depois da 6ª, só voltam pelo botão abaixo, que devolve
                todas para a fila com as tentativas zeradas (não lê na hora e não apaga nada).
              </p>
              <Button
                variante="outline"
                className="mt-2"
                disabled={ocupado}
                onClick={() => {
                  const emFalha = s.coletores.reduce((t, c) => t + c.falhas, 0);
                  acao(rearmar.mutateAsync().then(() => setRearmadas(emFalha)));
                }}
              >
                <RotateCcw className="mr-2 h-4 w-4" />
                Tentar todas de novo
              </Button>
            </div>
          ) : null}

          {/* `?? []`: o painel sobe antes do servidor no deploy, e o servidor velho não manda a lista. */}
          {(s.deVoltaNaFila ?? []).length > 0 ? (
            <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
              <p className="font-medium">De volta na fila (falharam antes e vão ser tentadas de novo):</p>
              <ul className="mt-1 space-y-0.5">
                {(s.deVoltaNaFila ?? []).map((f) => (
                  <li key={`volta-${f.coletor}-${f.inicio}-${f.escopo}`}>
                    {descreverLeitura(f)} — última tentativa {dataHora(f.tentadaEm)}: {f.erro}
                  </li>
                ))}
              </ul>
              <p className="mt-2 text-amber-800">{quandoVaiLer(s)}</p>
            </div>
          ) : null}
        </>
      ) : status.isLoading ? (
        <Loader2 className="h-4 w-4 animate-spin text-gray-400" />
      ) : null}

      {erro ? (
        <p className="mt-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700">{erro}</p>
      ) : null}
    </section>
  );
}
