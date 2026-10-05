import { useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  Building2,
  CheckCircle2,
  ExternalLink,
  FileCheck2,
  Link2,
  Loader2,
} from 'lucide-react';
import {
  useGerarRequisicaoSiscan,
  usePreparoSiscan,
  type OpcaoSiscan,
  type RequisicaoSiscan,
  type UnidadeSiscan,
} from '@/features/anamnese/api/siscanApi';
import { perdeuSessaoSiscan, recusaPorCadastroCadsus } from '@/features/anamnese/lib/sessaoSiscan';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { ConfirmDialog } from '@/shared/ui/ConfirmDialog';
import { Modal } from '@/shared/ui/Modal';

/**
 * Onde se corrige o cadastro nacional da paciente. É o mesmo destino do botão "Corrigir/Atualizar
 * dados do paciente no CADSUSWEB" da tela do SISCAN.
 */
const URL_CADSUSWEB = 'https://cadastro.saude.gov.br/novocartao/';

/**
 * Gerar a requisição do exame no SISCAN, a partir da anamnese.
 *
 * <p><b>Confirmar antes de mandar.</b> O modal percorre o assistente do SISCAN sem gravar e
 * mostra, pergunta a pergunta, o que será enviado. É escrita em produção federal, sobre a saúde
 * de uma pessoa: quem clica precisa ver o que está afirmando.</p>
 *
 * <p><b>O responsável é escolhido aqui, e não na anamnese</b>, por uma razão técnica com
 * consequência prática: a lista de profissionais só existe dentro do SISCAN, depois de o
 * assistente saber a unidade e o tipo de mamografia — e ela <b>muda</b> entre diagnóstica e
 * rastreamento. O CNS, que é como eles identificam, não existe no nosso cadastro.</p>
 *
 * <p><b>O desfecho fica no modal</b>, não num toast de quatro segundos: o número do protocolo é o
 * que a médica vai usar para laudar.</p>
 *
 * <p><b>Unidade fora da conta.</b> Quando a unidade do pedido não está entre as unidades
 * requisitantes que a conta do SISCAN do operador enxerga, o preparo devolve a lista delas e o
 * operador escolhe por qual enviar — e <b>confirma</b> a escolha num diálogo antes de qualquer
 * consulta. Confirmar refaz o preparo com a unidade (os responsáveis são por unidade); a
 * requisição só nasce no "Gerar requisição", e a escolha fica registrada na anamnese.</p>
 *
 * <p><b>Raça/Cor que falta no CADSUS.</b> O SISCAN troca o campo travado por um combo e não
 * avança sem ele (medido em 30/09/2026). O modal mostra as opções DELE, a pessoa escolhe
 * perguntando à paciente (é autodeclaração) e confirma; Indígena pede também a etnia. Vai para o
 * SISCAN e fica registrado na anamnese.</p>
 *
 * <p><b>Outros dados do CADSUS</b> (nome da mãe, endereço…) o painel ainda não preenche: a recusa
 * diz qual campo e leva ao CADSUSWEB.</p>
 */
export function ModalGerarRequisicaoSiscan({
  aberto,
  exameImagemId,
  pacienteId,
  pacienteNascimento,
  aoFechar,
  aoGerar,
  aoPerderSessao,
}: {
  aberto: boolean;
  exameImagemId: string;
  /** Paciente do pedido — o nome aparece no padrão do painel (idade + resumo). Sem ele, só o texto. */
  pacienteId?: string | null;
  /** Data de nascimento (aaaa-mm-dd), quando quem abre já a tem — a idade sai dela. */
  pacienteNascimento?: string | null;
  aoFechar: () => void;
  aoGerar?: (resultado: RequisicaoSiscan) => void;
  /**
   * A sessão do operador no SISCAN não existe mais (API reiniciou, 8 h paradas, senha recusada
   * ao relogar). Quem abriu o modal reabre o login e, autenticado, abre este de novo (#137).
   */
  aoPerderSessao?: () => void;
}) {
  // Só preenchida quando a unidade do pedido não está na conta do SISCAN e a pessoa escolheu outra.
  const [cnesUnidade, setCnesUnidade] = useState<string>('');
  // Só preenchidas quando a paciente está sem Raça/Cor no CADSUS e o SISCAN pediu.
  const [racaCor, setRacaCor] = useState<string>('');
  const [etnia, setEtnia] = useState<string>('');
  // Escolhido num combo e ainda NÃO confirmado: nada é consultado até a pessoa confirmar.
  const [pendente, setPendente] = useState<Pendente | null>(null);
  const escolhas = useMemo(
    () => ({
      cnesUnidade: cnesUnidade || undefined,
      racaCor: racaCor || undefined,
      etnia: etnia || undefined,
    }),
    [cnesUnidade, racaCor, etnia],
  );
  const preparo = usePreparoSiscan(exameImagemId, aberto, escolhas);
  const gerar = useGerarRequisicaoSiscan(exameImagemId);

  // Sessão perdida não é erro para mostrar em vermelho e fechar: é "entre de novo". Sem isto a
  // pessoa lia a mensagem, fechava, e não sabia que bastava logar outra vez.
  useEffect(() => {
    if (aberto && preparo.isError && perdeuSessaoSiscan(preparo.error)) aoPerderSessao?.();
  }, [aberto, preparo.isError, preparo.error, aoPerderSessao]);

  const [cnsEscolhido, setCnsEscolhido] = useState<string>('');
  const [erro, setErro] = useState<string | null>(null);
  const [erroCadsus, setErroCadsus] = useState(false);
  const [resultado, setResultado] = useState<RequisicaoSiscan | null>(null);
  // Vincular e criar terminam no mesmo POST (o backend decide), mas o que a pessoa fez é
  // diferente — e a tela não pode dizer "criada" quando ela só trouxe o que já existia.
  const [foiVinculo, setFoiVinculo] = useState(false);

  // A sugestão da máquina entra pré-selecionada, mas quem confirma é gente: os nomes não batem
  // na forma entre a ficha do SISREG e o SISCAN ("FERNANDA SOUZA" × "FERNANDA SOUZA LEITE").
  useEffect(() => {
    if (preparo.data?.cnsResponsavelSugerido) setCnsEscolhido(preparo.data.cnsResponsavelSugerido);
  }, [preparo.data?.cnsResponsavelSugerido]);

  useEffect(() => {
    if (aberto) {
      setErro(null);
      setErroCadsus(false);
      setResultado(null);
      setCnesUnidade('');
      setRacaCor('');
      setEtnia('');
      setPendente(null);
    }
  }, [aberto]);

  /** Toda escolha refaz a conferência — o responsável escolhido antes pode não valer mais. */
  function antesDeEscolher() {
    setCnsEscolhido('');
    setErro(null);
    setErroCadsus(false);
  }

  function aplicarPendente(p: Pendente) {
    antesDeEscolher();
    if (p.tipo === 'unidade') setCnesUnidade(p.unidade.cnes);
    if (p.tipo === 'raca') {
      setRacaCor(p.opcao.codigo);
      setEtnia(''); // trocou a raça: a etnia (só de Indígena) não vale mais
    }
    if (p.tipo === 'etnia') setEtnia(p.opcao.codigo);
  }

  function refazerEscolhas() {
    antesDeEscolher();
    setCnesUnidade('');
    setRacaCor('');
    setEtnia('');
  }

  async function confirmar(vinculo = false) {
    setErro(null);
    setErroCadsus(false);
    setFoiVinculo(vinculo);
    try {
      const gerada = await gerar.mutateAsync({ cnsResponsavel: cnsEscolhido, ...escolhas });
      setResultado(gerada);
      aoGerar?.(gerada);
    } catch (falha) {
      // Nada foi gravado quando a sessão falta (o backend recusa antes do POST): relogar e voltar.
      if (perdeuSessaoSiscan(falha) && aoPerderSessao) {
        aoPerderSessao();
        return;
      }
      setErro(extrairMensagemDeErro(falha));
      setErroCadsus(recusaPorCadastroCadsus(falha));
    }
  }

  const dados = preparo.data;
  const temLacunas = (dados?.lacunas.length ?? 0) > 0;
  const jaLa = dados?.encontradaPeloProntuario ?? null;
  const duplicidades = dados?.duplicidades ?? [];
  const unidadesDisponiveis = dados?.unidadesDisponiveis ?? [];
  // A unidade do pedido não está na conta: sem escolher outra, não há responsável nem Gerar.
  const precisaUnidade = unidadesDisponiveis.length > 0;
  const faltaUnidade = precisaUnidade && !dados?.unidadeEscolhida;
  // A paciente sem Raça/Cor no CADSUS: sem informar (e a etnia, se Indígena), o SISCAN não avança.
  const opcoesRaca = dados?.racaCorOpcoes ?? [];
  const opcoesEtnia = dados?.etniaOpcoes ?? [];
  const precisaRaca = opcoesRaca.length > 0;
  const faltaRaca =
    precisaRaca &&
    (!dados?.racaCorEscolhida || (opcoesEtnia.length > 0 && !dados?.etniaEscolhida));
  const temEscolha = Boolean(cnesUnidade || racaCor || etnia);

  return (
    <Modal
      aberto={aberto}
      aoFechar={aoFechar}
      titulo="Gerar requisição no SISCAN"
      descricao="A requisição é criada no sistema do Ministério da Saúde, em nome da unidade que solicitou o exame."
      largura="lg"
    >
      {preparo.isPending ? (
        <div className="flex items-center justify-center py-10 text-gray-500">
          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
          {temEscolha
            ? 'Refazendo a conferência no SISCAN com o que você escolheu…'
            : 'Consultando o SISCAN…'}
        </div>
      ) : preparo.isError ? (
        <div className="space-y-3">
          {recusaPorCadastroCadsus(preparo.error) ? (
            <AvisoCadastroCadsus mensagem={extrairMensagemDeErro(preparo.error)} />
          ) : (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {extrairMensagemDeErro(preparo.error)}
            </div>
          )}
          <div className="flex justify-end gap-2">
            <Button variante="secundaria" onClick={aoFechar}>
              Fechar
            </Button>
            {temEscolha ? (
              <Button variante="secundaria" onClick={refazerEscolhas}>
                Refazer as escolhas
              </Button>
            ) : null}
            {/* SISCAN que caiu por ociosidade reconecta sozinho na próxima tentativa. */}
            <Button onClick={() => void preparo.refetch()}>Tentar de novo</Button>
          </div>
        </div>
      ) : resultado ? (
        <div className="space-y-4">
          <div className="flex items-start gap-3 rounded-md border border-emerald-200 bg-emerald-50 px-3 py-3">
            <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" />
            <div className="text-sm text-emerald-900">
              <p className="font-semibold">
                {foiVinculo
                  ? 'Requisição vinculada ao pedido.'
                  : 'Requisição criada no SISCAN.'}
              </p>
              <p className="mt-1">
                Protocolo <span className="font-mono font-semibold">{resultado.protocolo}</span>
                {resultado.numeroExame ? (
                  <>
                    {' '}
                    · Nº do exame{' '}
                    <span className="font-mono font-semibold">{resultado.numeroExame}</span>
                  </>
                ) : null}
              </p>
              <p className="mt-1 text-xs">
                São dois números diferentes, e o SISCAN pesquisa pelos dois. Responsável:{' '}
                {resultado.responsavelNome}.
              </p>
            </div>
          </div>
          {resultado.unidadeEscolhida ? (
            <div className="flex items-start gap-3 rounded-md border border-sky-200 bg-sky-50 px-3 py-3">
              <Building2 className="mt-0.5 h-5 w-5 shrink-0 text-sky-600" />
              <div className="text-sm text-sky-900">
                <p className="font-semibold">
                  Enviada pela unidade {resultado.unidadeEscolhida.nome}
                </p>
                <p className="mt-1">
                  A unidade do pedido, {resultado.unidadeEscolhida.nomeDoPedido} (CNES{' '}
                  {resultado.unidadeEscolhida.cnesDoPedido}), não está na sua conta do SISCAN. A
                  troca ficou registrada na anamnese.
                </p>
              </div>
            </div>
          ) : null}
          {resultado.racaCor ? (
            <div className="flex items-start gap-3 rounded-md border border-sky-200 bg-sky-50 px-3 py-3">
              <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-sky-600" />
              <div className="text-sm text-sky-900">
                <p className="font-semibold">
                  Raça/Cor informada ao SISCAN: {resultado.racaCor.rotulo}
                  {resultado.racaCor.etniaRotulo ? ` · etnia ${resultado.racaCor.etniaRotulo}` : ''}
                </p>
                <p className="mt-1">
                  Faltava no cadastro nacional da paciente (CADSUS). Ficou registrado na anamnese.
                </p>
              </div>
            </div>
          ) : null}
          {resultado.correcaoAnoUltimaMamografia ? (
            /* Mudamos uma resposta da anamnese sem a pessoa ter clicado nela: isso tem que ficar
               à vista aqui, no desfecho, e não num toast que some. */
            <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-3">
              <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
              <div className="text-sm text-amber-900">
                <p className="font-semibold">
                  Corrigimos o ano da última mamografia:{' '}
                  {resultado.correcaoAnoUltimaMamografia.anoDeclarado} →{' '}
                  {resultado.correcaoAnoUltimaMamografia.anoNoSiscan}
                </p>
                <p className="mt-1">
                  A anamnese dizia{' '}
                  <strong>{resultado.correcaoAnoUltimaMamografia.anoDeclarado}</strong>, mas o
                  SISCAN já tem mamografia desta paciente no SUS em{' '}
                  <strong>{resultado.correcaoAnoUltimaMamografia.anoNoSiscan}</strong> — e ele não
                  aceita ano anterior ao que tem registrado. O ano informado estava errado.
                </p>
                <p className="mt-1">
                  Já corrigimos a anamnese para{' '}
                  {resultado.correcaoAnoUltimaMamografia.anoNoSiscan} e enviamos{' '}
                  {resultado.correcaoAnoUltimaMamografia.anoNoSiscan} ao SISCAN.
                </p>
              </div>
            </div>
          ) : null}
          <div className="flex justify-end">
            <Button onClick={aoFechar}>Fechar</Button>
          </div>
        </div>
      ) : jaLa ? (
        /* A requisição DESTE pedido já está no SISCAN e não estava carimbada aqui — foi o que
           aconteceu com as criadas fora do painel. Criar outra duplicaria a paciente lá. */
        <div className="space-y-4">
          <div className="flex items-start gap-3 rounded-md border border-sky-200 bg-sky-50 px-3 py-3">
            <Link2 className="mt-0.5 h-5 w-5 shrink-0 text-sky-600" />
            <div className="text-sm text-sky-900">
              <p className="font-semibold">Este pedido já tem requisição no SISCAN.</p>
              <p className="mt-1">
                Ela foi encontrada pelo Nº do Prontuário — que é o número deste pedido. Falta só
                trazer os números para cá.
              </p>
              <p className="mt-2">
                Protocolo <span className="font-mono font-semibold">{jaLa.protocolo}</span> · Nº do
                exame <span className="font-mono font-semibold">{jaLa.numeroExame}</span>
              </p>
              <p className="mt-1 text-xs">
                {jaLa.unidade} · {jaLa.status}
                {jaLa.datas ? ` · ${jaLa.datas}` : ''}
              </p>
            </div>
          </div>
          {erro ? (
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erro}
            </div>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button variante="secundaria" onClick={aoFechar} disabled={gerar.isPending}>
              Fechar
            </Button>
            <Button onClick={() => confirmar(true)} disabled={gerar.isPending}>
              {gerar.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Link2 className="mr-2 h-4 w-4" />
              )}
              Vincular ao pedido
            </Button>
          </div>
        </div>
      ) : duplicidades.length > 0 ? (
        /* A paciente já tem requisição na janela, e não é deste pedido. O sistema não escolhe
           qual vale: isso é trabalho de gente, no SISCAN. */
        <div className="space-y-4">
          <div className="flex items-start gap-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-3">
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
            <div className="text-sm text-amber-900">
              <p className="font-semibold">
                Esta paciente já tem requisição de mamografia no SISCAN.
              </p>
              <p className="mt-1">
                Encontrada no último ano, e ela <strong>não é deste pedido</strong>. Criar outra
                deixaria duas requisições abertas para a mesma pessoa.
              </p>
            </div>
          </div>
          <div className="overflow-hidden rounded-md border border-gray-200">
            <table className="w-full text-sm">
              <thead className="bg-gray-50 text-xs uppercase tracking-wide text-gray-500">
                <tr>
                  <th className="px-3 py-2 text-left font-medium">Protocolo</th>
                  <th className="px-3 py-2 text-left font-medium">Nº do exame</th>
                  <th className="px-3 py-2 text-left font-medium">Unidade</th>
                  <th className="px-3 py-2 text-left font-medium">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {duplicidades.map((d) => (
                  <tr key={`${d.protocolo}-${d.numeroExame}`}>
                    <td className="px-3 py-2 font-mono">{d.protocolo}</td>
                    <td className="px-3 py-2 font-mono">{d.numeroExame}</td>
                    <td className="px-3 py-2 text-gray-600">{d.unidade}</td>
                    <td className="px-3 py-2 text-gray-600">{d.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="text-xs text-gray-500">
            Resolva no SISCAN qual delas vale — e o que fazer com a outra — antes de criar uma nova
            por aqui.
          </p>
          <div className="flex justify-end">
            <Button onClick={aoFechar}>Entendi</Button>
          </div>
        </div>
      ) : dados?.jaGerada ? (
        <div className="space-y-4">
          <div className="rounded-md border border-gray-200 bg-gray-50 px-3 py-3 text-sm text-gray-700">
            <p className="font-semibold">Este exame já tem requisição no SISCAN.</p>
            <p className="mt-1">
              Protocolo <span className="font-mono">{dados.protocolo}</span>
              {dados.numeroExame ? (
                <>
                  {' '}
                  · Nº do exame <span className="font-mono">{dados.numeroExame}</span>
                </>
              ) : null}
            </p>
            <p className="mt-1 text-xs text-gray-500">
              Gerar de novo criaria uma requisição duplicada para a mesma paciente.
            </p>
          </div>
          <div className="flex justify-end">
            <Button onClick={aoFechar}>Fechar</Button>
          </div>
        </div>
      ) : dados ? (
        <div className="space-y-4">
          <dl className="grid grid-cols-1 gap-x-6 gap-y-1 text-sm sm:grid-cols-2">
            <div className="flex justify-between gap-3 sm:block">
              <dt className="text-gray-500">Paciente</dt>
              <dd className="font-medium text-gray-900">
                {pacienteId ? (
                  <NomePacienteComResumo
                    pacienteId={pacienteId}
                    nome={dados.pacienteNome}
                    nascimento={pacienteNascimento}
                  />
                ) : (
                  dados.pacienteNome
                )}
              </dd>
            </div>
            <div className="flex justify-between gap-3 sm:block">
              <dt className="text-gray-500">
                {precisaUnidade ? 'Unidade do pedido' : 'Unidade requisitante'}
              </dt>
              <dd className="font-medium text-gray-900">
                {dados.unidadeNome}{' '}
                <span className="font-mono text-xs text-gray-500">CNES {dados.cnesUnidade}</span>
              </dd>
            </div>
            <div className="flex justify-between gap-3 sm:block">
              <dt className="text-gray-500">Tipo de mamografia</dt>
              <dd className="font-medium text-gray-900">
                {dados.tipoMamografiaRotulo}{' '}
                <span className="text-xs text-gray-500">(pela idade da paciente)</span>
              </dd>
            </div>
          </dl>

          {precisaUnidade ? (
            /* A unidade do pedido não está entre as que a conta do SISCAN enxerga. Antes, isto era
               um erro sem saída; agora a pessoa escolhe por qual unidade enviar, e fica registrado. */
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-3 text-sm text-amber-900">
              <p className="flex items-start gap-2 font-semibold">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                A unidade do pedido não está na sua conta do SISCAN
              </p>
              <p className="mt-1">
                {dados.unidadeNome} (CNES {dados.cnesUnidade}) não está entre as unidades
                requisitantes que a sua conta do SISCAN enxerga. Escolha por qual unidade enviar — a
                escolha fica registrada na anamnese.
              </p>
              <label className="mt-2 block font-medium">
                Enviar pela unidade
                <select
                  value={cnesUnidade}
                  onChange={(e) => {
                    // Não consulta nada ainda: abre a confirmação. Cancelar mantém a anterior.
                    const u = unidadesDisponiveis.find((x) => x.cnes === e.target.value);
                    if (u) setPendente({ tipo: 'unidade', unidade: u });
                  }}
                  className="mt-1 w-full rounded-md border border-amber-300 bg-white px-2 py-1.5 text-sm text-gray-900"
                >
                  <option value="">Selecione…</option>
                  {unidadesDisponiveis.map((u) => (
                    <option key={u.cnes} value={u.cnes}>
                      {u.nome} — CNES {u.cnes}
                    </option>
                  ))}
                </select>
              </label>
            </div>
          ) : null}

          {precisaRaca ? (
            <BlocoRacaCor
              opcoesRaca={opcoesRaca}
              racaCor={racaCor}
              opcoesEtnia={opcoesEtnia}
              etnia={etnia}
              aoEscolherRaca={(o) => setPendente({ tipo: 'raca', opcao: o })}
              aoEscolherEtnia={(o) => setPendente({ tipo: 'etnia', opcao: o })}
            />
          ) : null}

          {dados.avisoData ? (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
              <p className="flex items-start gap-2">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                <span>{dados.avisoData}</span>
              </p>
            </div>
          ) : null}

          {temLacunas ? (
            <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
              <p className="flex items-start gap-2 font-semibold">
                <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                Falta responder na anamnese
              </p>
              <ul className="mt-1 list-disc space-y-0.5 pl-6 text-xs">
                {dados.lacunas.map((l) => (
                  <li key={l.campo}>{l.pergunta}</li>
                ))}
              </ul>
            </div>
          ) : null}

          <div>
            <label className="block text-sm font-medium text-gray-700">
              Responsável pela requisição
              <select
                value={cnsEscolhido}
                onChange={(e) => setCnsEscolhido(e.target.value)}
                disabled={faltaUnidade || faltaRaca}
                className="mt-1 w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm disabled:bg-gray-100"
              >
                <option value="">Selecione…</option>
                {dados.responsaveis.map((r) => (
                  <option key={r.cns} value={r.cns}>
                    {r.nome}
                  </option>
                ))}
              </select>
            </label>
            <p className="mt-1 text-xs text-gray-500">
              {faltaRaca
                ? 'Informe primeiro a Raça/Cor — sem ela o SISCAN não avança para a lista de responsáveis.'
                : faltaUnidade
                ? 'Escolha primeiro a unidade — a lista de responsáveis do SISCAN é por unidade.'
                : dados.responsaveis.length === 0
                ? 'O SISCAN não ofereceu nenhum profissional para esta unidade e este tipo de mamografia.'
                : dados.nomeSolicitanteDaFicha
                  ? `Quem pediu o exame no SISREG foi ${dados.nomeSolicitanteDaFicha}. A lista é do SISCAN e muda entre diagnóstica e rastreamento — confira o nome antes de confirmar.`
                  : 'A lista é do SISCAN e muda entre diagnóstica e rastreamento.'}
            </p>
          </div>

          <div>
            <p className="text-sm font-medium text-gray-700">O que será enviado</p>
            <div className="mt-1 max-h-56 overflow-y-auto rounded-md border border-gray-200">
              <table className="w-full text-sm">
                <tbody>
                  {dados.envio.map((c, i) => (
                    <tr key={i} className="border-b border-gray-100 last:border-0">
                      <td className="px-3 py-1.5 text-gray-600">{c.pergunta}</td>
                      <td className="px-3 py-1.5 text-right font-medium text-gray-900">
                        {c.resposta}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="mt-1 text-xs text-gray-500">
              As respostas vêm da anamnese. Onde ela não perguntou, vai “Não sabe” — que é uma
              resposta do próprio SISCAN, e não um campo preenchido por conta.
            </p>
          </div>

          {erro ? (
            erroCadsus ? (
              <AvisoCadastroCadsus mensagem={erro} />
            ) : (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {erro}
              </div>
            )
          ) : null}

          <div className="flex items-center justify-between gap-3">
            <p className="text-xs text-gray-500">
              Isto grava no sistema do Ministério da Saúde e não tem desfazer.
            </p>
            <div className="flex gap-2">
              <Button variante="secundaria" onClick={aoFechar} disabled={gerar.isPending}>
                Cancelar
              </Button>
              <Button
                onClick={() => confirmar()}
                disabled={gerar.isPending || temLacunas || faltaUnidade || faltaRaca || !cnsEscolhido}
              >
                {gerar.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <FileCheck2 className="mr-2 h-4 w-4" />
                )}
                Gerar requisição
              </Button>
            </div>
          </div>
        </div>
      ) : null}

      <ConfirmDialog
        aberto={pendente !== null}
        titulo={pendente ? TITULO_PENDENTE[pendente.tipo] : ''}
        mensagem={pendente ? mensagemPendente(pendente, dados?.unidadeNome) : ''}
        rotuloConfirmar={pendente ? CONFIRMAR_PENDENTE[pendente.tipo] : 'Confirmar'}
        aoConfirmar={() => {
          if (pendente) aplicarPendente(pendente);
          setPendente(null);
        }}
        aoCancelar={() => setPendente(null)}
      />
    </Modal>
  );
}

/** Uma escolha feita num combo e ainda à espera da confirmação. */
type Pendente =
  | { tipo: 'unidade'; unidade: UnidadeSiscan }
  | { tipo: 'raca'; opcao: OpcaoSiscan }
  | { tipo: 'etnia'; opcao: OpcaoSiscan };

const TITULO_PENDENTE: Record<Pendente['tipo'], string> = {
  unidade: 'Enviar por outra unidade?',
  raca: 'Informar a Raça/Cor?',
  etnia: 'Informar a etnia?',
};

const CONFIRMAR_PENDENTE: Record<Pendente['tipo'], string> = {
  unidade: 'Usar esta unidade',
  raca: 'Informar esta Raça/Cor',
  etnia: 'Informar esta etnia',
};

const NADA_GRAVADO =
  'Ao confirmar, o sistema só refaz a conferência no SISCAN — nada é gravado até você clicar em "Gerar requisição".';

function mensagemPendente(p: Pendente, unidadeDoPedido: string | undefined): string {
  if (p.tipo === 'unidade') {
    return `A requisição vai sair no SISCAN em nome de ${p.unidade.nome} (CNES ${p.unidade.cnes}), e não da unidade do pedido${unidadeDoPedido ? `, ${unidadeDoPedido}` : ''}. ${NADA_GRAVADO}`;
  }
  if (p.tipo === 'raca') {
    return `${p.opcao.rotulo} vai para o SISCAN como a Raça/Cor da paciente. É autodeclarada: confirme que foi ela quem disse. ${NADA_GRAVADO}`;
  }
  return `${p.opcao.rotulo} vai para o SISCAN como a etnia da paciente, declarada por ela. ${NADA_GRAVADO}`;
}

/**
 * A paciente sem Raça/Cor no CADSUS: as opções são as do SISCAN (o código é o dele). Indígena
 * abre a etnia — 475 opções, por isso com filtro.
 */
function BlocoRacaCor({
  opcoesRaca,
  racaCor,
  opcoesEtnia,
  etnia,
  aoEscolherRaca,
  aoEscolherEtnia,
}: {
  opcoesRaca: OpcaoSiscan[];
  racaCor: string;
  opcoesEtnia: OpcaoSiscan[];
  etnia: string;
  aoEscolherRaca: (o: OpcaoSiscan) => void;
  aoEscolherEtnia: (o: OpcaoSiscan) => void;
}) {
  const [filtro, setFiltro] = useState('');
  const etniasVisiveis = useMemo(() => {
    const f = semAcento(filtro.trim());
    const lista = f ? opcoesEtnia.filter((o) => semAcento(o.rotulo).includes(f)) : opcoesEtnia;
    // A escolhida continua na lista mesmo fora do filtro — senão o combo "perde" a seleção.
    const escolhida = opcoesEtnia.find((o) => o.codigo === etnia);
    return escolhida && !lista.includes(escolhida) ? [escolhida, ...lista] : lista;
  }, [filtro, opcoesEtnia, etnia]);

  return (
    <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-3 text-sm text-amber-900">
      <p className="flex items-start gap-2 font-semibold">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
        A paciente está sem Raça/Cor no cadastro nacional
      </p>
      <p className="mt-1">
        O SISCAN pede que ela seja informada aqui. É <strong>autodeclarada</strong>: pergunte à
        paciente. O que for escolhido vai para o SISCAN e fica registrado na anamnese.
      </p>
      <label className="mt-2 block font-medium">
        Raça/Cor
        <select
          value={racaCor}
          onChange={(e) => {
            const o = opcoesRaca.find((x) => x.codigo === e.target.value);
            if (o) aoEscolherRaca(o);
          }}
          className="mt-1 w-full rounded-md border border-amber-300 bg-white px-2 py-1.5 text-sm text-gray-900"
        >
          <option value="">Selecione…</option>
          {opcoesRaca.map((o) => (
            <option key={o.codigo} value={o.codigo}>
              {o.rotulo}
            </option>
          ))}
        </select>
      </label>
      {opcoesEtnia.length > 0 ? (
        <div className="mt-2">
          <label className="block font-medium">
            Etnia
            <input
              type="text"
              value={filtro}
              onChange={(e) => setFiltro(e.target.value)}
              placeholder="Digite parte do nome para filtrar…"
              className="mt-1 w-full rounded-md border border-amber-300 bg-white px-2 py-1.5 text-sm text-gray-900"
            />
          </label>
          <select
            value={etnia}
            onChange={(e) => {
              const o = opcoesEtnia.find((x) => x.codigo === e.target.value);
              if (o) aoEscolherEtnia(o);
            }}
            aria-label="Etnia"
            className="mt-1 w-full rounded-md border border-amber-300 bg-white px-2 py-1.5 text-sm text-gray-900"
          >
            <option value="">
              {etniasVisiveis.length === 0 ? 'Nenhuma etnia com esse nome' : 'Selecione…'}
            </option>
            {etniasVisiveis.map((o) => (
              <option key={o.codigo} value={o.codigo}>
                {o.rotulo}
              </option>
            ))}
          </select>
        </div>
      ) : null}
    </div>
  );
}

function semAcento(t: string): string {
  return t.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase();
}

/**
 * Recusa por outro dado que vem do CADSUS (nome da mãe, endereço…) — Raça/Cor não cai aqui, tem
 * combo próprio. O painel ainda não preenche esses campos: o que resolve é corrigir o cadastro
 * nacional e gerar de novo.
 */
function AvisoCadastroCadsus({ mensagem }: { mensagem: string }) {
  return (
    <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-3 text-sm text-amber-900">
      <p className="flex items-start gap-2 font-semibold">
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
        Falta um dado no cadastro nacional da paciente (CADSUS)
      </p>
      <p className="mt-1">{mensagem}</p>
      <p className="mt-2 text-xs">
        Nada foi gravado no SISCAN. Depois de corrigir no CADSUSWEB, abra de novo o “Gerar
        requisição”.
      </p>
      <a
        href={URL_CADSUSWEB}
        target="_blank"
        rel="noopener noreferrer"
        className="mt-2 inline-flex items-center gap-1.5 rounded-md border border-amber-300 bg-white px-3 py-1.5 text-sm font-medium text-amber-900 hover:bg-amber-100"
      >
        <ExternalLink className="h-4 w-4" />
        Abrir o CADSUSWEB
      </a>
    </div>
  );
}
