import { AlertTriangle, RefreshCw, WifiOff } from 'lucide-react';
import type { Painel } from '@/types/painel';
import { usePainel } from '@/lib/usePainel';
import { horaMinuto, nomeDoMes } from '@/lib/formatos';
import { Header } from '@/components/Header';
import {
  SkeletonPainel,
  SkeletonSecaoAgora,
  SkeletonSecaoGraficos,
  SkeletonSecaoPulseiras,
} from '@/components/Skeletons';
import { SecaoAgora } from '@/sections/SecaoAgora';
import { SecaoEmergencia } from '@/sections/SecaoEmergencia';
import { SecaoAtendimentos } from '@/sections/SecaoAtendimentos';
import { SecaoInternacoes } from '@/sections/SecaoInternacoes';
import { RodapeMetodologia } from '@/sections/RodapeMetodologia';

function EstadoSemConexao({ aoTentar }: { aoTentar: () => void }) {
  return (
    <div className="flex flex-col items-center gap-4 py-24 text-center">
      <span className="flex h-14 w-14 items-center justify-center rounded-full bg-painel text-grafite">
        <WifiOff className="h-6 w-6" aria-hidden="true" />
      </span>
      <div>
        <p className="font-display text-lg font-bold text-tinta">Sem conexão com o Salux</p>
        <p className="mt-1 max-w-sm text-[14px] text-grafite">
          Não foi possível carregar os números do hospital. A atualização automática
          continua tentando a cada minuto.
        </p>
      </div>
      <button
        type="button"
        onClick={aoTentar}
        className="inline-flex items-center gap-2 rounded-full border border-linha bg-papel px-4 py-2 text-[13.5px] font-semibold text-tinta transition-colors hover:border-vermelho-marica/40"
      >
        <RefreshCw className="h-4 w-4" aria-hidden="true" />
        Tentar novamente
      </button>
    </div>
  );
}

/** Última leitura boa do Oracle: ultimaAtualizacaoOk, senão o atualizadoEm mais recente. */
function referenciaOracle(dados: Painel): string {
  if (dados.oracle.ultimaAtualizacaoOk) return dados.oracle.ultimaAtualizacaoOk;
  const carimbos = [
    dados.agora?.atualizadoEm,
    dados.atendimentos?.atualizadoEm,
    dados.internacoes?.atualizadoEm,
    dados.esperaPorCor?.atualizadoEm,
  ].filter((c): c is string => c != null);
  if (carimbos.length === 0) return dados.geradoEm;
  return carimbos.reduce((max, c) => (new Date(c) > new Date(max) ? c : max));
}

export default function App() {
  const { dados, usandoMock, erroRede, carregandoInicial, recarregar } = usePainel();

  return (
    <div className="flex min-h-screen flex-col">
      <Header dados={dados} usandoMock={usandoMock} />

      {erroRede && dados && (
        <div className="border-b border-triagem-amarelo/30 bg-triagem-amarelo/10">
          <p className="mx-auto flex max-w-pagina items-center gap-2 px-4 py-2 text-[13px] font-medium text-triagem-amarelo-apoio sm:px-6">
            <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
            Sem conexão com o Salux — mostrando dados de {horaMinuto(dados.geradoEm)}.
          </p>
        </div>
      )}

      {!erroRede && dados && !dados.oracle.ok && (
        <div className="border-b border-triagem-amarelo/30 bg-triagem-amarelo/10">
          <p className="mx-auto flex max-w-pagina items-center gap-2 px-4 py-2 text-[13px] font-medium text-triagem-amarelo-apoio sm:px-6">
            <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden="true" />
            Sem dados novos do Salux desde {horaMinuto(referenciaOracle(dados))}.
          </p>
        </div>
      )}

      <main className="mx-auto w-full max-w-pagina flex-1 px-4 pb-16 pt-6 sm:px-6">
        {carregandoInicial && <SkeletonPainel />}
        {!carregandoInicial && !dados && <EstadoSemConexao aoTentar={recarregar} />}
        {dados && (
          <div className="space-y-10 sm:space-y-12">
            {/* Cada seção renderiza de forma independente — no cold start o back
                responde 200 só com "agora"; seção ausente vira skeleton, nunca
                erro global com o back saudável. */}
            <div className="anima-entrada">
              {dados.agora ? (
                <SecaoAgora agora={dados.agora} internacoesHoje={dados.internacoes?.hoje} />
              ) : (
                <SkeletonSecaoAgora />
              )}
            </div>
            <div className="anima-entrada" style={{ animationDelay: '70ms' }}>
              {dados.esperaPorCor ? (
                <SecaoEmergencia
                  espera={dados.esperaPorCor}
                  rotuloMesAtual={
                    dados.atendimentos ? nomeDoMes(dados.atendimentos.mesAtual.rotulo) : 'mês atual'
                  }
                  rotuloMesAnterior={
                    dados.atendimentos
                      ? nomeDoMes(dados.atendimentos.mesAnterior.rotulo)
                      : 'mês anterior'
                  }
                />
              ) : (
                <SkeletonSecaoPulseiras />
              )}
            </div>
            <div className="anima-entrada" style={{ animationDelay: '140ms' }}>
              {dados.atendimentos ? (
                <SecaoAtendimentos atendimentos={dados.atendimentos} />
              ) : (
                <SkeletonSecaoGraficos />
              )}
            </div>
            <div className="anima-entrada" style={{ animationDelay: '210ms' }}>
              {dados.internacoes ? (
                <SecaoInternacoes internacoes={dados.internacoes} />
              ) : (
                <SkeletonSecaoGraficos />
              )}
            </div>
          </div>
        )}
      </main>

      {dados && <RodapeMetodologia fonte={dados.fonte} />}
    </div>
  );
}
