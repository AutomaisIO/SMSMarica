import { useEffect, useState } from 'react';
import { AlertTriangle, CheckCircle2, FileCheck2, Link2, Loader2 } from 'lucide-react';
import {
  useGerarRequisicaoSiscan,
  usePreparoSiscan,
  type RequisicaoSiscan,
} from '@/features/anamnese/api/siscanApi';
import { perdeuSessaoSiscan } from '@/features/anamnese/lib/sessaoSiscan';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';

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
 */
export function ModalGerarRequisicaoSiscan({
  aberto,
  exameImagemId,
  aoFechar,
  aoGerar,
  aoPerderSessao,
}: {
  aberto: boolean;
  exameImagemId: string;
  aoFechar: () => void;
  aoGerar?: (resultado: RequisicaoSiscan) => void;
  /**
   * A sessão do operador no SISCAN não existe mais (API reiniciou, 8 h paradas, senha recusada
   * ao relogar). Quem abriu o modal reabre o login e, autenticado, abre este de novo (#137).
   */
  aoPerderSessao?: () => void;
}) {
  const preparo = usePreparoSiscan(exameImagemId, aberto);
  const gerar = useGerarRequisicaoSiscan(exameImagemId);

  // Sessão perdida não é erro para mostrar em vermelho e fechar: é "entre de novo". Sem isto a
  // pessoa lia a mensagem, fechava, e não sabia que bastava logar outra vez.
  useEffect(() => {
    if (aberto && preparo.isError && perdeuSessaoSiscan(preparo.error)) aoPerderSessao?.();
  }, [aberto, preparo.isError, preparo.error, aoPerderSessao]);

  const [cnsEscolhido, setCnsEscolhido] = useState<string>('');
  const [erro, setErro] = useState<string | null>(null);
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
      setResultado(null);
    }
  }, [aberto]);

  async function confirmar(vinculo = false) {
    setErro(null);
    setFoiVinculo(vinculo);
    try {
      const gerada = await gerar.mutateAsync(cnsEscolhido);
      setResultado(gerada);
      aoGerar?.(gerada);
    } catch (falha) {
      // Nada foi gravado quando a sessão falta (o backend recusa antes do POST): relogar e voltar.
      if (perdeuSessaoSiscan(falha) && aoPerderSessao) {
        aoPerderSessao();
        return;
      }
      setErro(extrairMensagemDeErro(falha));
    }
  }

  const dados = preparo.data;
  const temLacunas = (dados?.lacunas.length ?? 0) > 0;
  const jaLa = dados?.encontradaPeloProntuario ?? null;
  const duplicidades = dados?.duplicidades ?? [];

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
          Consultando o SISCAN…
        </div>
      ) : preparo.isError ? (
        <div className="space-y-3">
          <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            {extrairMensagemDeErro(preparo.error)}
          </div>
          <div className="flex justify-end gap-2">
            <Button variante="secundaria" onClick={aoFechar}>
              Fechar
            </Button>
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
              <dd className="font-medium text-gray-900">{dados.pacienteNome}</dd>
            </div>
            <div className="flex justify-between gap-3 sm:block">
              <dt className="text-gray-500">Unidade requisitante</dt>
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
                className="mt-1 w-full rounded-md border border-gray-300 px-2 py-1.5 text-sm"
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
              {dados.responsaveis.length === 0
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
            <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              {erro}
            </div>
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
                disabled={gerar.isPending || temLacunas || !cnsEscolhido}
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
    </Modal>
  );
}
