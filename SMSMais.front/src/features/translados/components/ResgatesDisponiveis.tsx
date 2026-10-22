import { useState } from 'react';
import { Accessibility, Hand, Loader2, MapPin, Undo2, Users } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import {
  useAvisarNoLocal,
  useDesmarcarResgate,
  useMarcarResgates,
  useResgates,
} from '@/features/translados/api/queries';
import type {
  CanalAvisoResgate,
  CarroAgora,
  DisponibilidadeResgate,
  ResgateDisponivel,
} from '@/features/translados/types';

function hhmm(t: string | null): string | null {
  return t ? t.slice(0, 5) : null;
}

function horaDe(iso: string | null): string | null {
  return iso ? new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : null;
}

const AVISO: Record<CanalAvisoResgate, string> = {
  Paciente: 'avisado pelo paciente',
  Central: 'avisado pela central',
  Motorista: 'visto por um motorista',
};

/** "25 min", "1 h 05". */
function duracao(minutos: number): string {
  if (minutos < 60) return `${minutos} min`;
  return `${Math.floor(minutos / 60)} h ${String(minutos % 60).padStart(2, '0')}`;
}

/** O que a lista diz da disponibilidade (o mesmo texto do tablet). */
export function textoDisponibilidade(p: {
  disponibilidade: DisponibilidadeResgate;
  disponivelAs: string | null;
  disponivelEmMinutos: number | null;
  aguardandoMinutos?: number | null;
}): { texto: string; cor: string } {
  switch (p.disponibilidade) {
    case 'NoLocal':
      return {
        texto: p.aguardandoMinutos != null ? `Aguardando há ${duracao(p.aguardandoMinutos)}` : 'Aguardando no local',
        cor: 'bg-emerald-50 text-emerald-800 border-emerald-200',
      };
    case 'PelaPrevisao':
      return { texto: `Disponível pela previsão (${hhmm(p.disponivelAs)})`, cor: 'bg-amber-50 text-amber-800 border-amber-200' };
    case 'EmAtendimento':
      return {
        texto: `Disponível em ${p.disponivelEmMinutos} min (às ${hhmm(p.disponivelAs)})`,
        cor: 'bg-gray-50 text-gray-700 border-gray-200',
      };
    default:
      return { texto: 'Sem previsão: aguardando o aviso', cor: 'bg-gray-50 text-gray-600 border-gray-200' };
  }
}

/**
 * O que sobra do carro levando `quem` (cada paciente e os acompanhantes dele); `null` quando não cabem
 * juntos. A mesma regra da central (AcomodacaoNoVeiculo), que confere de novo ao marcar: cadeirante só
 * em assento de cadeirante; maca só em ambulância, uma por vez; isolamento não divide o carro.
 */
export function levando(carro: CarroAgora, quem: ResgateDisponivel[]): CarroAgora | null {
  const cadeira = (q: ResgateDisponivel) => q.mobilidade === 'CadeiranteVeiculoAdaptado';
  const maca = (q: ResgateDisponivel) => q.mobilidade === 'Maca';
  if (carro.pacientesABordo + quem.length > 1 && (carro.isolamentoABordo || quem.some((q) => q.isolamento))) {
    return null;
  }
  if ((carro.macaABordo ? 1 : 0) + quem.filter(maca).length > (carro.ambulancia ? 1 : 0)) return null;
  let { cadeirante: cad, passageiro: pas, acompanhante: aco } = carro;
  // `n` lugares, primeiro do tipo `a` e, sem ele, do `b`; falso quando não há.
  function tirar(n: number, a: 'cad' | 'pas' | 'aco', b: 'cad' | 'pas' | 'aco' | null): boolean {
    const vagas = { cad, pas, aco };
    const doA = Math.min(n, vagas[a]);
    vagas[a] -= doA;
    const resto = n - doA;
    if (resto > 0) {
      if (!b || resto > vagas[b]) return false;
      vagas[b] -= resto;
    }
    ({ cad, pas, aco } = vagas);
    return true;
  }
  // Primeiro quem tem lugar certo (cadeira, maca); o banco comum fica para os outros.
  if (!tirar(quem.filter(cadeira).length, 'cad', null)) return null;
  if (!tirar(quem.filter(maca).length, 'cad', 'pas')) return null;
  if (!tirar(quem.filter((q) => !cadeira(q) && !maca(q)).length, 'pas', 'aco')) return null;
  if (!tirar(quem.reduce((n, q) => n + q.acompanhantes, 0), 'aco', 'pas')) return null;
  return {
    ...carro,
    cadeirante: cad,
    passageiro: pas,
    acompanhante: aco,
    pacientesABordo: carro.pacientesABordo + quem.length,
    isolamentoABordo: carro.isolamentoABordo || quem.some((q) => q.isolamento),
    macaABordo: carro.macaABordo || quem.some(maca),
  };
}

function livresDe(c: CarroAgora): number {
  return c.passageiro + c.acompanhante + c.cadeirante;
}

/** Por que `p` não cabe junto com os escolhidos. */
function motivoJunto(carro: CarroAgora, p: ResgateDisponivel, escolhidos: ResgateDisponivel[]): string {
  const sobra = levando(carro, escolhidos) ?? carro;
  if (sobra.isolamentoABordo) return 'Há um paciente em isolamento no carro (ou escolhido): ninguém mais embarca com ele.';
  if (p.isolamento && sobra.pacientesABordo > 0) return 'Paciente em isolamento só viaja sozinho (com os acompanhantes dele).';
  if (p.mobilidade === 'Maca') {
    return carro.ambulancia ? 'A ambulância já leva um paciente de maca.' : 'Paciente de maca só vai de ambulância.';
  }
  if (p.mobilidade === 'CadeiranteVeiculoAdaptado' && sobra.cadeirante === 0) {
    return 'Não sobra assento de cadeirante neste carro.';
  }
  const pessoas = 1 + p.acompanhantes;
  const livres = livresDe(sobra);
  return livres === 0
    ? 'O carro fica cheio com quem já foi escolhido.'
    : `Não cabe: ${pessoas === 1 ? '1 pessoa' : `${pessoas} pessoas`} para ${livres === 1 ? '1 lugar livre' : `${livres} lugares livres`}.`;
}

function distancia(m: number | null): string | null {
  if (m == null) return null;
  return m < 1000 ? `${m} m` : `${(m / 1000).toFixed(1).replace('.', ',')} km`;
}

/**
 * "Pacientes disponíveis para resgate" (decisões de 19/10): a mesma lista que os motoristas veem no
 * tablet. Quem desceu na unidade aparece com a hora em que fica pronto pela previsão; quem já está
 * esperando no local (avisado pelo paciente, pela central ou por um motorista) vem primeiro. A central
 * marca quem esta rota vai buscar — só passa se couber no carro agora — e avisa que o paciente chegou.
 */
export function ResgatesDisponiveis({ rotaId }: { rotaId: string }) {
  const podeVer = usePermissao('Translados', 'Consulta');
  const podeMarcar = usePermissao('Translados', 'Edicao');
  const lista = useResgates(rotaId, podeVer);
  const marcar = useMarcarResgates();
  const desmarcar = useDesmarcarResgate();
  const avisar = useAvisarNoLocal();
  const [escolhidos, setEscolhidos] = useState<string[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  if (!podeVer) return null;

  async function executar(acao: () => Promise<unknown>) {
    setErro(null);
    try {
      await acao();
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  function alternar(p: ResgateDisponivel) {
    setEscolhidos((atual) => (atual.includes(p.sessaoId) ? atual.filter((x) => x !== p.sessaoId) : [...atual, p.sessaoId]));
  }

  const dados = lista.data;
  const disponiveis = dados?.disponiveis ?? [];
  const marcados = dados?.marcados ?? [];
  const validos = escolhidos.filter((id) => disponiveis.some((d) => d.sessaoId === id));
  // Os lugares livres caem a cada paciente escolhido (ele e os acompanhantes), e quem não cabe junto com
  // os escolhidos fica sem poder ser marcado.
  const escolhidosLista = disponiveis.filter((d) => validos.includes(d.sessaoId));
  const sobra = dados?.carro ? levando(dados.carro, escolhidosLista) : null;
  const livresAgora = sobra ? livresDe(sobra) : (dados?.lugaresLivres ?? null);
  const cadeiranteAgora = sobra ? sobra.cadeirante : (dados?.lugaresCadeiranteLivres ?? 0);
  const pessoasEscolhidas = escolhidosLista.reduce((n, p) => n + 1 + p.acompanhantes, 0);
  function motivoNaoPode(p: ResgateDisponivel): string | null {
    if (validos.includes(p.sessaoId)) return null;
    if (p.cabe === false) return p.motivoNaoCabe ?? 'Não cabe agora.';
    const carro = dados?.carro;
    if (!carro || levando(carro, [...escolhidosLista, p])) return null;
    return motivoJunto(carro, p, escolhidosLista);
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
      <div className="mb-1 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Hand className="h-4 w-4 text-gray-500" />
          <h2 className="text-sm font-semibold text-gray-900">Pacientes disponíveis para resgate</h2>
        </div>
        {livresAgora != null ? (
          <span className="text-xs text-gray-600">
            Lugares livres agora neste carro: <strong>{livresAgora}</strong>
            {cadeiranteAgora ? ` (${cadeiranteAgora} de cadeirante)` : ''}
            {pessoasEscolhidas > 0
              ? ` · escolhidos: ${pessoasEscolhidas === 1 ? '1 pessoa' : `${pessoasEscolhidas} pessoas`}`
              : ''}
          </span>
        ) : null}
      </div>
      <p className="mb-3 text-xs text-gray-500">
        A mesma lista dos tablets: todo carro pode resgatar, mesmo quem não levou. Quem já está esperando no local
        vem primeiro. Marque quem esta rota vai buscar: só passa se o paciente e os acompanhantes couberem no carro
        agora.
      </p>
      {erro ? (
        <div className="mb-3 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
      ) : null}

      {marcados.length > 0 ? (
        <div className="mb-4">
          <h3 className="mb-1 text-xs font-semibold uppercase tracking-wide text-gray-500">Marcados para esta rota</h3>
          <ul className="divide-y divide-gray-100 rounded-md border border-gray-100">
            {marcados.map((m) => (
              <li key={m.sessaoId} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
                <div className="min-w-0">
                  <div className="font-medium text-gray-900">{m.pacienteNome}</div>
                  <div className="text-xs text-gray-500">
                    {m.unidadeNome}
                    {m.acompanhantes > 0 ? ` · com ${m.acompanhantes === 1 ? '1 acompanhante' : `${m.acompanhantes} acompanhantes`}` : ''}
                  </div>
                </div>
                <span className={`rounded-full border px-2 py-0.5 text-xs ${textoDisponibilidade(m).cor}`}>
                  {textoDisponibilidade(m).texto}
                </span>
                {podeMarcar ? (
                  <Button
                    variante="outline"
                    tamanho="sm"
                    disabled={desmarcar.isPending}
                    onClick={() => executar(() => desmarcar.mutateAsync({ rotaId, sessaoId: m.sessaoId }))}
                  >
                    <Undo2 className="mr-1.5 h-3.5 w-3.5" /> Desmarcar
                  </Button>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {lista.isLoading ? (
        <p className="flex items-center gap-2 text-sm text-gray-500">
          <Loader2 className="h-4 w-4 animate-spin" /> Carregando…
        </p>
      ) : disponiveis.length === 0 ? (
        <p className="text-sm text-gray-500">Ninguém esperando resgate agora.</p>
      ) : (
        <ul className="divide-y divide-gray-100 rounded-md border border-gray-100">
          {disponiveis.map((p) => {
            const d = textoDisponibilidade(p);
            const motivo = motivoNaoPode(p);
            const naoCabe = motivo !== null;
            return (
              <li key={p.sessaoId} className="flex flex-wrap items-center gap-3 px-3 py-2 text-sm">
                {podeMarcar ? (
                  <input
                    type="checkbox"
                    aria-label={`Marcar ${p.pacienteNome} para resgate`}
                    className="h-4 w-4"
                    checked={escolhidos.includes(p.sessaoId)}
                    disabled={naoCabe}
                    onChange={() => alternar(p)}
                  />
                ) : null}
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-1.5 font-medium text-gray-900">
                    {p.pacienteNome}
                    <span className="font-normal text-gray-400">·</span>
                    <span className="font-semibold text-gray-700">{p.unidadeNome}</span>
                    {p.mobilidade === 'CadeiranteVeiculoAdaptado' ? (
                      <Accessibility className="h-3.5 w-3.5 text-gray-500" aria-label="Cadeirante" />
                    ) : null}
                    {p.acompanhantes > 0 ? (
                      <span className="inline-flex items-center gap-0.5 text-xs font-normal text-gray-500">
                        <Users className="h-3.5 w-3.5" /> +{p.acompanhantes}
                      </span>
                    ) : null}
                  </div>
                  <div className="text-xs text-gray-500">
                    <MapPin className="mr-0.5 inline h-3 w-3" />
                    {p.foraDoMunicipio ? 'Fora do município' : 'Em Maricá'}
                    {p.disponibilidade === 'NoLocal' && p.noLocalDesde
                      ? ` · no local desde ${horaDe(p.noLocalDesde)}${p.avisadoPor ? ` (${AVISO[p.avisadoPor]})` : ''}`
                      : ''}
                    {distancia(p.distanciaMetros) ? ` · ${distancia(p.distanciaMetros)} do carro` : ''}
                    {p.veiculoDaIda ? ` · levado por ${p.veiculoDaIda}` : ''}
                  </div>
                  {p.aindaEmAtendimentoEm && p.disponibilidade !== 'NoLocal' ? (
                    <div className="text-xs text-gray-600">
                      Disse às {horaDe(p.aindaEmAtendimentoEm)} que ainda não terminou; o app pergunta de novo em 10 min.
                    </div>
                  ) : null}
                  {p.naoEstavaNoLocalEm ? (
                    <div className="text-xs text-amber-700">Não estava no local às {horaDe(p.naoEstavaNoLocalEm)}.</div>
                  ) : null}
                  {motivo ? <div className="text-xs text-red-700">{motivo}</div> : null}
                </div>
                <span className={`rounded-full border px-2 py-0.5 text-xs ${d.cor}`}>{d.texto}</span>
                {podeMarcar && p.disponibilidade !== 'NoLocal' ? (
                  <Button
                    variante="outline"
                    tamanho="sm"
                    disabled={avisar.isPending}
                    onClick={() => executar(() => avisar.mutateAsync({ rotaId, sessaoId: p.sessaoId }))}
                  >
                    Avisar que está no local
                  </Button>
                ) : null}
              </li>
            );
          })}
        </ul>
      )}

      {podeMarcar && disponiveis.length > 0 ? (
        <div className="mt-3 flex justify-end">
          <Button
            disabled={validos.length === 0 || marcar.isPending}
            onClick={() =>
              executar(async () => {
                await marcar.mutateAsync({ rotaId, sessaoIds: validos });
                setEscolhidos([]);
              })
            }
          >
            {marcar.isPending ? <Loader2 className="mr-1.5 h-4 w-4 animate-spin" /> : null}
            {validos.length <= 1 ? 'Marcar para resgate nesta rota' : `Marcar ${validos.length} para resgate nesta rota`}
          </Button>
        </div>
      ) : null}
    </section>
  );
}
