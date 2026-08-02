import { useState } from 'react';
import { CalendarClock, CheckCircle2, FileWarning, XCircle } from 'lucide-react';
import { cn } from '@/shared/lib/cn';
import { usePainelInicio } from '@/features/painel-inicio/api/queries';
import type {
  DirecaoPainel,
  ItemPendenciaImportacaoPainel,
  LenteEscopo,
} from '@/features/painel-inicio/types';
import { Raia } from '@/features/painel-inicio/components/Raia';
import { LinhaSolicitacao } from '@/features/painel-inicio/components/LinhaSolicitacao';
import { LinhaPendencia } from '@/features/painel-inicio/components/LinhaPendencia';
import { ModalInformarCpf } from '@/features/painel-inicio/components/ModalInformarCpf';

const DIRECOES: { id: DirecaoPainel; rotulo: string }[] = [
  { id: 'Tudo', rotulo: 'Tudo' },
  { id: 'Executante', rotulo: 'Como executante' },
  { id: 'Solicitante', rotulo: 'Como solicitante' },
];

/**
 * O painel da tela de início: o que precisa da atenção do operador agora.
 *
 * Três raias acionáveis + um número tranquilo. Nada mais entra sem ADR, e raia com zero não
 * renderiza — é assim que o painel continua sendo lido nos dias em que tem algo. As regras estão
 * em `docs/modulos/painel-inicio/ux.md`; leia antes de acrescentar qualquer coisa aqui.
 */
export function PainelInicio() {
  const [lente, setLente] = useState<LenteEscopo>('Unidade');
  const [direcao, setDirecao] = useState<DirecaoPainel>('Tudo');
  const [pendenciaAlvo, setPendenciaAlvo] = useState<ItemPendenciaImportacaoPainel | null>(null);

  const { data, isLoading, isError } = usePainelInicio(lente, direcao);

  // Primeira carga: não ocupa a tela com esqueleto. O grid de módulos aparece na hora.
  if (isLoading || isError || !data) return null;

  const noMunicipio = data.lente === 'Municipio';
  const temAlgo =
    (data.cancelados?.total ?? 0) > 0 ||
    (data.aguardando?.total ?? 0) > 0 ||
    (data.pendenciasImportacao?.total ?? 0) > 0;
  const temConfirmados = data.confirmados !== null;

  // Sem nenhuma raia e sem permissão para os números: o painel inteiro some, e a home volta a
  // ser o grid de módulos de sempre.
  if (!temAlgo && !temConfirmados) return null;

  return (
    <section className="space-y-3">
      {(data.podeAlternarLente || !noMunicipio) && (
        <div className="flex flex-wrap items-center gap-2">
          {/* A lente só aparece para quem tem visão global. Para os demais NÃO é um botão
              desabilitado: ausência não gera a pergunta "por que não posso?". */}
          {data.podeAlternarLente && (
            <Seletor
              opcoes={[
                { id: 'Unidade', rotulo: data.unidadeReferenciaNome ?? 'Minhas unidades' },
                { id: 'Municipio', rotulo: 'Município' },
              ]}
              valor={lente}
              aoTrocar={setLente}
            />
          )}

          {/* O recorte executante × solicitante só faz sentido com uma unidade de referência. */}
          {!noMunicipio && data.unidadeReferenciaId && (
            <Seletor opcoes={DIRECOES} valor={direcao} aoTrocar={setDirecao} />
          )}
        </div>
      )}

      {data.cancelados && (
        <Raia
          titulo="Cancelou pelo WhatsApp"
          subtitulo="o paciente avisou que não vem — a vaga pode ser reaproveitada"
          total={data.cancelados.total}
          tom="vermelho"
          icone={XCircle}
          verTodosPara="/app/solicitacoes-exame?painel=cancelados"
        >
          {data.cancelados.itens.map((i) => (
            <LinhaSolicitacao key={i.id} item={i} />
          ))}
        </Raia>
      )}

      {data.aguardando && (
        <Raia
          titulo="Aguardando resposta"
          subtitulo={`exame em até ${data.janelaAguardandoDias} dias e o paciente ainda não respondeu`}
          total={data.aguardando.total}
          tom="ambar"
          icone={CalendarClock}
          verTodosPara="/app/solicitacoes-exame?painel=aguardando"
        >
          {data.aguardando.itens.map((i) => (
            <LinhaSolicitacao key={i.id} item={i} />
          ))}
        </Raia>
      )}

      {data.pendenciasImportacao && (
        <Raia
          titulo="Pendências de importação"
          subtitulo="linhas do SISREG que NÃO entraram no sistema"
          total={data.pendenciasImportacao.total}
          tom="roxo"
          icone={FileWarning}
          verTodosPara="/app/importacao-sisreg?aba=erros"
        >
          {data.pendenciasImportacao.itens.map((i) => (
            <LinhaPendencia key={i.id} item={i} aoInformarCpf={setPendenciaAlvo} />
          ))}
        </Raia>
      )}

      {data.confirmados && (
        <p className="flex items-center gap-2 px-1 text-sm text-gray-600">
          <CheckCircle2 className="h-4 w-4 text-success-600" />
          <span>
            Confirmados nos próximos {data.confirmados.janelaDias} dias:{' '}
            <strong className="tabular-nums text-gray-900">{data.confirmados.recebidos}</strong>
            {noMunicipio || !data.unidadeReferenciaId ? (
              ' no total'
            ) : (
              <>
                {' recebidos · '}
                <strong className="tabular-nums text-gray-900">{data.confirmados.enviados}</strong>
                {' enviados'}
              </>
            )}
          </span>
        </p>
      )}

      <ModalInformarCpf
        pendencia={
          pendenciaAlvo && {
            id: pendenciaAlvo.id,
            pacienteNome: pendenciaAlvo.pacienteNome,
            procedimento: pendenciaAlvo.procedimento,
            codigoSolicitacao: pendenciaAlvo.codigoSolicitacao,
          }
        }
        aoFechar={() => setPendenciaAlvo(null)}
      />
    </section>
  );
}

/** Grupo de botões segmentado — mesmo gesto para a lente e para a direção. */
function Seletor<T extends string>({
  opcoes,
  valor,
  aoTrocar,
}: {
  opcoes: { id: T; rotulo: string }[];
  valor: T;
  aoTrocar: (v: T) => void;
}) {
  return (
    <div className="inline-flex rounded-lg border border-gray-200 bg-white p-0.5">
      {opcoes.map((o) => (
        <button
          key={o.id}
          type="button"
          onClick={() => aoTrocar(o.id)}
          aria-pressed={valor === o.id}
          className={cn(
            'rounded-md px-2.5 py-1 text-xs font-medium transition',
            valor === o.id ? 'bg-primary-50 text-primary-700' : 'text-gray-600 hover:bg-gray-50',
          )}
        >
          {o.rotulo}
        </button>
      ))}
    </div>
  );
}
