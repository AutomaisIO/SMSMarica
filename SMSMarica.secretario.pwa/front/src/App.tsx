import { AlertTriangle, Download, RefreshCw, WifiOff } from 'lucide-react';
import type { Painel, UnidadeId, UnidadePainel, VisaoPainel } from '@/types/painel';
import { usePainel } from '@/lib/usePainel';
import { useUnidade } from '@/lib/useUnidade';
import { useVisao } from '@/lib/useVisao';
import { useVersaoApp } from '@/lib/useVersaoApp';
import { horaMinuto, nomeDoMes } from '@/lib/formatos';
import { Header } from '@/components/Header';
import { InstalarApp } from '@/components/InstalarApp';
import { SegmentedControl, type OpcaoSegmento } from '@/components/SegmentedControl';
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
import { SecaoMaternidade } from '@/sections/SecaoMaternidade';
import { SecaoLeitos } from '@/sections/SecaoLeitos';
import { Rodape } from '@/sections/Rodape';

const VISOES: OpcaoSegmento<VisaoPainel>[] = [
  { valor: 'emergencia', rotulo: 'Emergência' },
  { valor: 'leitos', rotulo: 'Leitos e internação' },
];

function EstadoSemConexao({ aoTentar }: { aoTentar: () => void }) {
  return (
    <div className="flex flex-col items-center gap-4 py-24 text-center">
      <span className="flex h-14 w-14 items-center justify-center rounded-full bg-painel text-grafite">
        <WifiOff className="h-6 w-6" aria-hidden="true" />
      </span>
      <div>
        <p className="font-display text-lg font-bold text-tinta">Sem conexão com as unidades</p>
        <p className="mt-1 max-w-sm text-[14px] text-grafite">
          Não foi possível carregar os números do hospital e da UPA. A atualização
          automática continua tentando a cada minuto.
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

/** Última leitura boa das bases: ultimaAtualizacaoOk, senão o carimbo mais recente. */
function referenciaFonte(dados: Painel): string {
  if (dados.status.ultimaAtualizacaoOk) return dados.status.ultimaAtualizacaoOk;
  const carimbos = dados.unidades
    .flatMap((u) => [
      u.agora?.atualizadoEm,
      u.atendimentos?.atualizadoEm,
      u.internacoes?.atualizadoEm,
      u.esperaPorCor?.atualizadoEm,
    ])
    .filter((c): c is string => c != null);
  if (carimbos.length === 0) return dados.geradoEm;
  return carimbos.reduce((max, c) => (new Date(c) > new Date(max) ? c : max));
}

/**
 * Quem está atrasado, pelo nome. Com duas bases, "sem dados novos" sem dizer de
 * QUAL unidade obriga o leitor a adivinhar de quem é o número velho na tela.
 */
function fontesComProblema(dados: Painel): string {
  const paradas = dados.fontes.filter((f) => !f.status.ok).map((f) => f.nome);
  return paradas.length > 0 ? paradas.join(' e ') : 'as unidades';
}

function ConteudoUnidade({ unidade, visao }: { unidade: UnidadePainel; visao: VisaoPainel }) {
  if (visao === 'leitos') {
    return unidade.leitos ? <SecaoLeitos leitos={unidade.leitos} /> : <SkeletonSecaoGraficos />;
  }

  return (
    <div className="space-y-10 sm:space-y-12">
      {/* Cada seção renderiza de forma independente — no cold start o back
          responde 200 só com "agora"; seção ausente vira skeleton, nunca
          erro global com o back saudável. */}
      <div className="anima-entrada">
        {unidade.agora ? (
          <SecaoAgora
            agora={unidade.agora}
            coresUsadas={unidade.coresUsadas}
            internacoesHoje={unidade.internacoes?.hoje}
          />
        ) : (
          <SkeletonSecaoAgora />
        )}
      </div>
      <div className="anima-entrada" style={{ animationDelay: '70ms' }}>
        {unidade.esperaPorCor ? (
          <SecaoEmergencia
            espera={unidade.esperaPorCor}
            coresUsadas={unidade.coresUsadas}
            consolidado={unidade.id === 'geral'}
            rotuloMesAtual={
              unidade.atendimentos ? nomeDoMes(unidade.atendimentos.mesAtual.rotulo) : 'mês atual'
            }
            rotuloMesAnterior={
              unidade.atendimentos
                ? nomeDoMes(unidade.atendimentos.mesAnterior.rotulo)
                : 'mês anterior'
            }
          />
        ) : (
          <SkeletonSecaoPulseiras />
        )}
      </div>
      <div className="anima-entrada" style={{ animationDelay: '140ms' }}>
        {unidade.atendimentos ? (
          <SecaoAtendimentos atendimentos={unidade.atendimentos} />
        ) : (
          <SkeletonSecaoGraficos />
        )}
      </div>
      {/* Internações e maternidade não existem na UPA: a seção some, em vez de
          aparecer zerada como se a unidade não tivesse internado ninguém. */}
      {unidade.internacoes && (
        <div className="anima-entrada" style={{ animationDelay: '210ms' }}>
          <SecaoInternacoes internacoes={unidade.internacoes} />
        </div>
      )}
      {unidade.maternidade && (
        <div className="anima-entrada" style={{ animationDelay: '280ms' }}>
          <SecaoMaternidade maternidade={unidade.maternidade} />
        </div>
      )}
    </div>
  );
}

export default function App() {
  const { dados, usandoMock, erroRede, carregandoInicial, recarregar } = usePainel();
  const { novaVersao } = useVersaoApp();
  const [unidadeId, escolherUnidade] = useUnidade();
  const [visao, escolherVisao] = useVisao();

  // A escolha guardada pode não existir no payload (unidade removida do back):
  // cai na primeira em vez de renderizar tela em branco.
  const unidade = dados?.unidades.find((u) => u.id === unidadeId) ?? dados?.unidades[0];

  const opcoes: OpcaoSegmento<UnidadeId>[] =
    dados?.unidades.map((u) => ({ valor: u.id, rotulo: u.rotulo })) ?? [];

  return (
    <div className="flex min-h-screen flex-col">
      <Header dados={dados} usandoMock={usandoMock} />

      {/* Cache nunca é problema do Secretário: o app se atualiza sozinho e só
          avisa que está fazendo isso. */}
      {novaVersao && (
        <div className="border-b border-vermelho-marica/20 bg-vermelho-marica/[0.06]">
          <p className="mx-auto flex max-w-pagina items-center gap-2 px-4 py-2 text-[13px] font-medium text-vermelho-marica sm:px-6">
            <Download className="h-4 w-4 shrink-0" aria-hidden="true" />
            Nova versão do painel — atualizando…
          </p>
        </div>
      )}

      {erroRede && dados && (
        <div className="border-b border-triagem-amarelo/30 bg-triagem-amarelo/10">
          <p className="mx-auto flex max-w-pagina items-center gap-2 px-4 py-2 text-[13px] font-medium text-triagem-amarelo-apoio sm:px-6">
            <WifiOff className="h-4 w-4 shrink-0" aria-hidden="true" />
            Sem conexão com o painel — mostrando dados de {horaMinuto(dados.geradoEm)}.
          </p>
        </div>
      )}

      {!erroRede && dados && !dados.status.ok && (
        <div className="border-b border-triagem-amarelo/30 bg-triagem-amarelo/10">
          <p className="mx-auto flex max-w-pagina items-center gap-2 px-4 py-2 text-[13px] font-medium text-triagem-amarelo-apoio sm:px-6">
            <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden="true" />
            Sem dados novos de {fontesComProblema(dados)} desde{' '}
            {horaMinuto(referenciaFonte(dados))}.
          </p>
        </div>
      )}

      <main className="mx-auto w-full max-w-pagina flex-1 px-4 pb-12 pt-5 sm:px-6">
        <div className="mb-5 empty:hidden">
          <InstalarApp />
        </div>
        {carregandoInicial && <SkeletonPainel />}
        {!carregandoInicial && !dados && <EstadoSemConexao aoTentar={recarregar} />}
        {dados && unidade && (
          <>
            {/* O seletor fica ACIMA de tudo e mostra o nome por extenso da unidade
                escolhida: num painel de rede, a pergunta "esse número é de onde?"
                não pode depender de lembrar qual pílula estava marcada. */}
            {/* Duas escolhas empilhadas: PRIMEIRO o assunto (emergência ou leitos),
                depois de quem. Invertido, o usuário troca de unidade e perde o
                assunto que estava olhando. */}
            <div className="mb-4">
              <SegmentedControl
                opcoes={VISOES}
                valor={visao}
                aoMudar={escolherVisao}
                ariaLabel="Assunto exibido no painel"
              />
            </div>

            <div className="mb-6 flex flex-wrap items-end justify-between gap-x-4 gap-y-3">
              <div>
                <p className="eyebrow">Unidade</p>
                <p className="mt-0.5 font-display text-[19px] font-bold leading-tight tracking-tight text-tinta sm:text-[21px]">
                  {unidade.nome}
                </p>
              </div>
              {opcoes.length > 1 && (
                <SegmentedControl
                  opcoes={opcoes}
                  valor={unidade.id}
                  aoMudar={escolherUnidade}
                  ariaLabel="Unidade exibida no painel"
                />
              )}
            </div>

            <ConteudoUnidade key={`${unidade.id}-${visao}`} unidade={unidade} visao={visao} />
          </>
        )}

        {/* Procedência do número, em uma linha — o rodapé é institucional. */}
        {unidade && (
          <p className="mt-8 text-[12.5px] text-grafite">
            {unidade.fonte} · atualização automática a cada minuto (momento) e 10 minutos
            (consolidados).
          </p>
        )}
      </main>

      <Rodape />
    </div>
  );
}
