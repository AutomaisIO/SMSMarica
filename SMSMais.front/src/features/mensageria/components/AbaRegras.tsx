import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import {
  useAlterarRegraUnidade,
  useConfiguracaoConfirmacao,
  useConfiguracaoMensageria,
  useRegrasUnidades,
  useSalvarConfiguracaoConfirmacao,
  useSalvarConfiguracaoMensageria,
} from '@/features/mensageria/api/queries';
import type { RegraUnidade } from '@/features/mensageria/types';

function numeroOuNull(v: string): number | null {
  const t = v.trim().replace(',', '.');
  if (t === '') return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : null;
}

/** Tarifas Meta (estimativa de custo) — só aparece na tela de Estatísticas para quem tem o módulo de custos. */
function SecaoTarifasMeta({ podeEditar }: { podeEditar: boolean }) {
  const cfg = useConfiguracaoMensageria();
  const salvar = useSalvarConfiguracaoMensageria();
  const [utility, setUtility] = useState('');
  const [marketing, setMarketing] = useState('');
  const [auth, setAuth] = useState('');
  const [mapa, setMapa] = useState('');

  useEffect(() => {
    if (!cfg.data) return;
    setUtility(cfg.data.tarifaUtilityUsd?.toString() ?? '');
    setMarketing(cfg.data.tarifaMarketingUsd?.toString() ?? '');
    setAuth(cfg.data.tarifaAuthenticationUsd?.toString() ?? '');
    setMapa(Object.entries(cfg.data.templatesCategorias).map(([t, c]) => `${t}=${c}`).join('\n'));
  }, [cfg.data]);

  function salvarTudo() {
    const templatesCategorias: Record<string, string> = {};
    for (const linha of mapa.split('\n')) {
      const [t, c] = linha.split('=').map((x) => x.trim());
      if (t && c) templatesCategorias[t] = c;
    }
    salvar.mutate({
      tarifaUtilityUsd: numeroOuNull(utility),
      tarifaMarketingUsd: numeroOuNull(marketing),
      tarifaAuthenticationUsd: numeroOuNull(auth),
      templatesCategorias,
    });
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4">
      <h2 className="text-base font-semibold text-gray-900">Tarifas Meta (estimativa de custo)</h2>
      <p className="mt-1 text-sm text-gray-600">
        Preço em USD por mensagem de template, por categoria de cobrança da Meta. Alimenta a estimativa de custo
        em Estatísticas (só para quem tem o módulo de custos). Texto de sessão e <em>utility</em> dentro de janela
        de 24h aberta não custam. Sem tarifa cadastrada, a estimativa fica zerada.
      </p>
      <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-3">
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Utility (USD)</span>
          <Input value={utility} onChange={(e) => setUtility(e.target.value)} placeholder="ex.: 0.0080" disabled={!podeEditar} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Marketing (USD)</span>
          <Input value={marketing} onChange={(e) => setMarketing(e.target.value)} placeholder="ex.: 0.0625" disabled={!podeEditar} />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Authentication (USD)</span>
          <Input value={auth} onChange={(e) => setAuth(e.target.value)} placeholder="ex.: 0.0315" disabled={!podeEditar} />
        </label>
      </div>
      <label className="mt-3 flex flex-col gap-1 text-sm">
        <span className="font-medium text-gray-700">Template → categoria (um por linha, <code>nome=utility|marketing|authentication</code>)</span>
        <textarea
          className="input min-h-[80px] font-mono text-xs"
          value={mapa}
          onChange={(e) => setMapa(e.target.value)}
          disabled={!podeEditar}
          placeholder={'authzap=authentication\nconfirmacao_regulacao=utility'}
        />
        <span className="text-xs text-gray-500">Sem entrada, o template conta como utility (nome com "auth" conta como authentication).</span>
      </label>
      {podeEditar ? (
        <div className="mt-3 flex items-center justify-end gap-3">
          {salvar.isError ? <span className="text-sm text-red-700">{extrairMensagemDeErro(salvar.error)}</span> : null}
          {salvar.isSuccess ? <span className="text-sm text-emerald-700">Salvo.</span> : null}
          <Button tamanho="sm" disabled={salvar.isPending} onClick={salvarTudo}>
            <Save className="mr-1.5 h-4 w-4" /> Salvar tarifas
          </Button>
        </div>
      ) : null}
    </section>
  );
}

/**
 * Horas cheias, apresentadas como relógio.
 *
 * <p>Seletor, e não <code>type="time"</code>, por duas razões concretas: o campo "Para às" aceita
 * <b>24</b> — fim do dia — que nenhum campo de hora do navegador consegue representar; e um campo
 * de hora deixaria escolher 8h<b>30</b>, minuto que o motor não tem como respeitar e que a tela
 * descartaria calada. O que não pode ser obedecido não deve poder ser digitado.</p>
 */
const HORAS_DO_DIA = Array.from({ length: 24 }, (_, i) => String(i));
const HORAS_FIM = Array.from({ length: 24 }, (_, i) => String(i + 1));

const rotuloHora = (h: string) => (h === '24' ? '24:00 (fim do dia)' : `${h.padStart(2, '0')}:00`);

export function AbaRegras() {
  const podeEditar = usePermissao('Confirmacoes', 'Edicao') || usePermissao('NotificacoesAgendamento', 'Edicao');
  const cfg = useConfiguracaoConfirmacao();
  const salvar = useSalvarConfiguracaoConfirmacao();
  const regras = useRegrasUnidades();
  const alterarUnidade = useAlterarRegraUnidade();

  const [inicio, setInicio] = useState('08:00');
  const [fim, setFim] = useState('18:00');
  const [vazao, setVazao] = useState(100);
  const [somenteSisreg, setSomenteSisreg] = useState(true);
  const [lembreteDiasAntes, setLembreteDiasAntes] = useState(2);
  const [lembreteHabilitado, setLembreteHabilitado] = useState(false);
  const [conciliacaoCancelamento, setConciliacaoCancelamento] = useState(false);
  const [avisoCancelamento, setAvisoCancelamento] = useState(false);
  // Texto, não número: `Number('')` é 0, e um campo apagado para redigitar viraria "lê a cada 0
  // minutos, das 0h às 0h" na legenda e um 400 no salvar. Guardar o que foi digitado deixa o campo
  // vazio de verdade enquanto a pessoa redigita, e a conversão acontece num lugar só, na hora de
  // validar.
  const [conciliacaoIntervalo, setConciliacaoIntervalo] = useState('10');
  const [conciliacaoInicio, setConciliacaoInicio] = useState('8');
  const [conciliacaoFim, setConciliacaoFim] = useState('18');
  const [conciliacaoFechamento, setConciliacaoFechamento] = useState('7');

  useEffect(() => {
    if (!cfg.data) return;
    setInicio(cfg.data.horaInicioEnvio);
    setFim(cfg.data.horaFimEnvio);
    setVazao(cfg.data.maximoPorPassagem);
    setSomenteSisreg(cfg.data.somenteSisreg);
    setLembreteDiasAntes(cfg.data.lembreteDiasAntes);
    setLembreteHabilitado(cfg.data.lembreteHabilitado);
    setConciliacaoCancelamento(cfg.data.conciliacaoCancelamentoHabilitada);
    setAvisoCancelamento(cfg.data.avisoCancelamentoHabilitado);
    setConciliacaoIntervalo(String(cfg.data.conciliacaoIntervaloMinutos));
    setConciliacaoInicio(String(cfg.data.conciliacaoHoraInicio));
    setConciliacaoFim(String(cfg.data.conciliacaoHoraFim));
    setConciliacaoFechamento(String(cfg.data.conciliacaoHoraFechamento));
  }, [cfg.data]);

  // As mesmas regras do backend, ditas antes do 400 — e apontando o campo, não o rodapé da tela.
  const conciliacao = useMemo(() => {
    const n = (t: string) => (/^\d+$/.test(t.trim()) ? Number(t) : null);
    const intervalo = n(conciliacaoIntervalo);
    const ini = n(conciliacaoInicio);
    const f = n(conciliacaoFim);
    const fech = n(conciliacaoFechamento);

    let erro: string | null = null;
    if (intervalo === null || ini === null || f === null || fech === null)
      erro = 'Preencha os quatro campos com números inteiros.';
    else if (intervalo < 1 || intervalo > 120) erro = 'A leitura deve ocorrer a cada 1 a 120 minutos.';
    else if (ini > 23 || f < 1 || f > 24 || fech > 23)
      erro = 'A leitura começa entre 0h e 23h, termina entre 1h e 24h, e o fechamento fica entre 0h e 23h.';
    else if (ini >= f) erro = 'A hora de início da leitura precisa ser anterior à de fim.';
    else if (f - ini >= 24)
      erro = 'A janela não pode cobrir o dia inteiro: o fechamento precisa de uma hora livre, fora dela.';
    else if (fech >= ini && fech < f)
      erro = `O fechamento relê o dia anterior e precisa ficar FORA da janela (${rotuloHora(String(ini))} às ${rotuloHora(String(f))}).`;

    return { intervalo, ini, f, fech, erro };
  }, [conciliacaoIntervalo, conciliacaoInicio, conciliacaoFim, conciliacaoFechamento]);

  const colunas: Coluna<RegraUnidade>[] = useMemo(
    () => [
      { chave: 'nome', cabecalho: 'Unidade executante', render: (u) => u.unidadeNome },
      {
        chave: 'aviso',
        cabecalho: 'Avisa o paciente',
        render: (u) => (
          <label className="inline-flex items-center gap-2">
            <input
              type="checkbox"
              checked={u.enviarConfirmacao}
              disabled={!podeEditar || alterarUnidade.isPending}
              onChange={(e) => alterarUnidade.mutate({ unidadeId: u.unidadeId, enviar: e.target.checked })}
            />
            <span className={u.enviarConfirmacao ? 'text-emerald-700' : 'text-gray-500'}>{u.enviarConfirmacao ? 'Ligado' : 'Desligado'}</span>
          </label>
        ),
      },
      { chave: 'procs', cabecalho: 'Procedimentos com aviso', render: (u) => `${u.procedimentosComAviso} de ${u.procedimentosTotal}` },
      {
        chave: 'link',
        cabecalho: '',
        render: (u) => (
          <Link to={`/app/unidades/${u.unidadeId}`} className="text-xs text-red-700 hover:underline">Escolher procedimentos</Link>
        ),
      },
    ],
    [podeEditar, alterarUnidade],
  );

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-gray-200 bg-white p-4">
        <h2 className="text-base font-semibold text-gray-900">Parâmetros de disparo</h2>
        <p className="mt-1 text-sm text-gray-600">
          Fora do horário, as confirmações ficam <strong>empilhadas</strong> e saem quando o horário abrir — a
          sincronização do SISREG pode rodar de madrugada sem ninguém receber mensagem de noite. A única exceção é a
          resposta a quem acabou de se identificar pelo WhatsApp (a pessoa está na conversa).
        </p>
        <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-4">
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-gray-700">Começa a enviar às</span>
            <Input type="time" value={inicio} onChange={(e) => setInicio(e.target.value)} disabled={!podeEditar} />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-gray-700">Para de enviar às</span>
            <Input type="time" value={fim} onChange={(e) => setFim(e.target.value)} disabled={!podeEditar} />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium text-gray-700">Mensagens por rodada (a cada minuto)</span>
            <Input type="number" min={1} max={1000} value={vazao} onChange={(e) => setVazao(Number(e.target.value))} disabled={!podeEditar} />
          </label>
          <label className="flex items-center gap-2 self-end pb-2 text-sm">
            <input type="checkbox" checked={somenteSisreg} onChange={(e) => setSomenteSisreg(e.target.checked)} disabled={!podeEditar} />
            <span>Só agendamentos do <strong>SISREG</strong></span>
          </label>
        </div>
        <div className="mt-5 border-t border-gray-100 pt-4">
          <h3 className="text-sm font-semibold text-gray-900">Lembrete antes do agendamento</h3>
          <p className="mt-1 text-sm text-gray-600">
            Uma segunda mensagem, às vésperas: um modelo para quem <strong>já confirmou</strong> (só
            para lembrar) e outro para quem <strong>ainda não respondeu</strong> (que continua
            oferecendo confirmar ou avisar que não vai). Vale para a rede toda — não é por unidade.
          </p>
          <div className="mt-3 grid grid-cols-1 gap-4 md:grid-cols-4">
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-medium text-gray-700">Dias antes do agendamento</span>
              <Input
                type="number"
                min={1}
                max={30}
                value={lembreteDiasAntes}
                onChange={(e) => setLembreteDiasAntes(Number(e.target.value))}
                disabled={!podeEditar}
              />
            </label>
            <label className="flex items-center gap-2 self-end pb-2 text-sm md:col-span-3">
              <input
                type="checkbox"
                checked={lembreteHabilitado}
                onChange={(e) => setLembreteHabilitado(e.target.checked)}
                disabled={!podeEditar}
              />
              <span>Enviar o lembrete</span>
            </label>
          </div>
        </div>

        {/*
          Cancelamento: dois interruptores separados de propósito. Dá para conciliar a base por
          alguns dias, conferir os números no log, e só então começar a avisar — ligar os dois
          juntos no primeiro dia é apostar que o volume diário é o esperado.
        */}
        <div className="mt-5 border-t border-gray-100 pt-4">
          <h3 className="text-sm font-semibold text-gray-900">Cancelamentos feitos no SISREG</h3>
          <p className="mt-0.5 text-xs text-gray-600">
            Cancelamento feito pela unidade executante, pela solicitante ou pela regulação não passa por
            aqui — e sem isto a vaga fica presa e o paciente segue sendo lembrado de um agendamento que
            já não existe.
          </p>
          <div className="mt-3 space-y-2">
            <label className="flex items-start gap-2 text-sm">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={conciliacaoCancelamento}
                onChange={(e) => setConciliacaoCancelamento(e.target.checked)}
                disabled={!podeEditar}
              />
              <span>
                Trazer os cancelamentos do SISREG para a base
                <span className="block text-xs text-gray-500">
                  {conciliacao.erro ? (
                    'Só leitura. Ajuste os campos abaixo para valer.'
                  ) : (
                    <>
                      Lê a cada {conciliacao.intervalo} minuto{conciliacao.intervalo === 1 ? '' : 's'}, das{' '}
                      {rotuloHora(String(conciliacao.ini))} às {rotuloHora(String(conciliacao.f))}, e relê o dia
                      anterior às {rotuloHora(String(conciliacao.fech))}. Só leitura.
                    </>
                  )}
                </span>
              </span>
            </label>

            {/*
              A cadência e a janela moram aqui porque o custo delas muda com a operação: apertar
              para 5 minutos dobra as requisições no SISREG, e o orçamento anti-robô é do operador.
              Quem estiver olhando o volume precisa poder afrouxar na hora — o motor relê a cada
              tick, então vale sem reiniciar nada.

              Os campos seguem editáveis com o motor DESLIGADO de propósito: desabilitá-los
              enquanto o valor em tela estivesse inválido travaria o salvamento da aba inteira
              (lembrete, horário de envio) por causa de um campo que a própria tela apresenta como
              inativo — e sem caminho para corrigi-lo a não ser religando o motor.
            */}
            <div className="grid grid-cols-2 gap-3 pl-6 md:grid-cols-4">
              <label className="flex flex-col gap-1 text-xs">
                <span className="font-medium text-gray-700">A cada (minutos)</span>
                <Input
                  type="number"
                  min={1}
                  max={120}
                  value={conciliacaoIntervalo}
                  onChange={(e) => setConciliacaoIntervalo(e.target.value)}
                  disabled={!podeEditar}
                />
              </label>
              <label className="flex flex-col gap-1 text-xs">
                <span className="font-medium text-gray-700">Começa às</span>
                <Select
                  value={conciliacaoInicio}
                  onChange={(e) => setConciliacaoInicio(e.target.value)}
                  disabled={!podeEditar}
                >
                  {HORAS_DO_DIA.map((h) => (
                    <option key={h} value={h}>
                      {rotuloHora(h)}
                    </option>
                  ))}
                </Select>
              </label>
              <label className="flex flex-col gap-1 text-xs">
                <span className="font-medium text-gray-700">Para às</span>
                <Select
                  value={conciliacaoFim}
                  onChange={(e) => setConciliacaoFim(e.target.value)}
                  disabled={!podeEditar}
                >
                  {HORAS_FIM.map((h) => (
                    <option key={h} value={h}>
                      {rotuloHora(h)}
                    </option>
                  ))}
                </Select>
              </label>
              <label className="flex flex-col gap-1 text-xs">
                <span className="font-medium text-gray-700">Fecha o dia anterior às</span>
                <Select
                  value={conciliacaoFechamento}
                  onChange={(e) => setConciliacaoFechamento(e.target.value)}
                  disabled={!podeEditar}
                >
                  {HORAS_DO_DIA.map((h) => (
                    <option key={h} value={h}>
                      {rotuloHora(h)}
                    </option>
                  ))}
                </Select>
                <span className="text-[11px] text-gray-500">Precisa ficar fora da janela acima.</span>
              </label>
            </div>
            {conciliacao.erro ? (
              <p className="pl-6 text-xs text-red-700">{conciliacao.erro}</p>
            ) : null}
            <label className="flex items-start gap-2 text-sm">
              <input
                type="checkbox"
                className="mt-0.5"
                checked={avisoCancelamento}
                onChange={(e) => setAvisoCancelamento(e.target.checked)}
                disabled={!podeEditar}
              />
              <span>
                Avisar o paciente do cancelamento
                <span className="block text-xs text-gray-500">
                  A mensagem diz que foi cancelado e nada mais — o motivo registrado no SISREG é interno.
                </span>
              </span>
            </label>
          </div>
        </div>

        {podeEditar ? (
          <div className="mt-4 flex items-center justify-end gap-3">
            {salvar.isError ? <span className="text-sm text-red-700">{extrairMensagemDeErro(salvar.error)}</span> : null}
            {salvar.isSuccess ? <span className="text-sm text-emerald-700">Salvo.</span> : null}
            <Button
              tamanho="sm"
              disabled={salvar.isPending || conciliacao.erro !== null}
              onClick={() =>
                salvar.mutate({
                  horaInicioEnvio: inicio,
                  horaFimEnvio: fim,
                  maximoPorPassagem: vazao,
                  somenteSisreg,
                  lembreteDiasAntes,
                  lembreteHabilitado,
                  conciliacaoCancelamentoHabilitada: conciliacaoCancelamento,
                  avisoCancelamentoHabilitado: avisoCancelamento,
                  conciliacaoIntervaloMinutos: conciliacao.intervalo!,
                  conciliacaoHoraInicio: conciliacao.ini!,
                  conciliacaoHoraFim: conciliacao.f!,
                  conciliacaoHoraFechamento: conciliacao.fech!,
                })
              }
            >
              <Save className="mr-1.5 h-4 w-4" /> Salvar parâmetros
            </Button>
          </div>
        ) : null}
      </section>

      <section className="space-y-3">
        <div>
          <h2 className="text-base font-semibold text-gray-900">Quem recebe o aviso</h2>
          <p className="mt-1 text-sm text-gray-600">
            A mensagem só sai quando a <strong>unidade executante</strong> está ligada <strong>e</strong> o procedimento
            também está marcado para avisar (os procedimentos se escolhem na aba SISREG da unidade).
          </p>
        </div>
        {alterarUnidade.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(alterarUnidade.error)}</p> : null}
        <Tabela colunas={colunas} dados={regras.data ?? []} chaveLinha={(u) => u.unidadeId} carregando={regras.isLoading} vazio="Nenhuma unidade com mapeamento SISREG." />
      </section>

      <SecaoTarifasMeta podeEditar={podeEditar} />

      <section className="rounded-lg border border-gray-200 bg-gray-50 p-4 text-sm text-gray-700">
        <h2 className="text-base font-semibold text-gray-900">Como a conversa acontece</h2>
        <ol className="mt-2 list-decimal space-y-1 pl-5">
          <li>
            Número <strong>ainda não verificado</strong>: primeiro pedimos os 4 primeiros dígitos do CPF, o mês/ano de
            nascimento e o nome. Quem responde <em>“Não sou essa pessoa”</em> e confirma que <em>não conhece</em> o
            paciente faz o número ficar marcado como <strong>inválido</strong> — nada mais é enviado para ele.
          </li>
          <li>
            Mensagem de confirmação com a data e a orientação de <strong>retirar a guia (ficha de solicitação) no posto</strong>{' '}
            e levar o <strong>pedido médico</strong>.
          </li>
          <li>
            O paciente confirma pelo link, ou toca em <em>“Não poderei ir!”</em> e escolhe <em>“Quero cancelar”</em> ou{' '}
            <em>“Não quero cancelar”</em>. O cancelamento vale na hora; o motivo é opcional.
          </li>
          <li>
            Quando uma <strong>atendente</strong> começa a tratar a solicitação no menu Confirmações, a mensagem automática
            que ainda não saiu <strong>não sai mais</strong> — a pessoa assume o contato.
          </li>
        </ol>
      </section>
    </div>
  );
}
