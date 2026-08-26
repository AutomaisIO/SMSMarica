import { useState } from 'react';
import { CheckCircle2, Download, Loader2, RefreshCw } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { hojeSP } from '@/shared/lib/datas';
import { Button } from '@/shared/ui/Button';
import { Modal } from '@/shared/ui/Modal';
import {
  useAlternarProcedimento,
  useAlternarProfissional,
  useImportarProcedimento,
} from '@/features/sisreg-mapeamento/api/queries';
import type { ImportacaoAgendaPontualResultado } from '@/features/sisreg-mapeamento/types';

type Props = {
  unidadeId: string;
  cpf: string;
  profissionalId: string;
  nomeProfissional: string;
  profissionalHabilitado: boolean;
  procedimentoId: string;
  codigoProcedimento: string;
  nomeProcedimento: string;
  procedimentoHabilitado: boolean;
  aoFechar: () => void;
};

/**
 * Import PONTUAL da agenda de HOJE de um procedimento — recurso PALIATIVO para quando o par
 * profissional × procedimento não está marcado para sincronizar automaticamente (não entra na
 * varredura diária). Só o dia de hoje, de propósito: cada importação custa requisição no SISREG, e
 * o SISREG bloqueia por volume — puxar um intervalo grande, à mão, aproximaria o CAPTCHA.
 *
 * <p>Consulta direta ao <c>cons_agendas</c> (não tem a trava de horário do <c>expo</c>, que é a
 * fonte da varredura noturna). Ao concluir, se o par ainda não sincroniza, oferece ativar o
 * sincronismo automático — e, se o operador aceitar, aplica e salva na hora.</p>
 */
export function ModalImportarProcedimento({
  unidadeId,
  cpf,
  profissionalId,
  nomeProfissional,
  profissionalHabilitado,
  procedimentoId,
  codigoProcedimento,
  nomeProcedimento,
  procedimentoHabilitado,
  aoFechar,
}: Props) {
  const hoje = hojeSP();
  const hojeBr = hoje.split('-').reverse().join('/');

  const [resultado, setResultado] = useState<ImportacaoAgendaPontualResultado | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [ativado, setAtivado] = useState(false);
  const [erroAtivar, setErroAtivar] = useState<string | null>(null);

  const importar = useImportarProcedimento(unidadeId);
  const alternarProf = useAlternarProfissional(unidadeId);
  const alternarProc = useAlternarProcedimento(unidadeId);

  // Só sugere ativar o sincronismo quando o par AINDA não está completo (profissional E
  // procedimento habilitados). Se já sincroniza, não há o que oferecer.
  const jaSincroniza = profissionalHabilitado && procedimentoHabilitado;
  const ativando = alternarProf.isPending || alternarProc.isPending;

  async function importarHoje() {
    setErro(null);
    try {
      const r = await importar.mutateAsync({
        cpf,
        codigoProcedimento,
        dataInicio: hoje,
        dataFim: hoje,
      });
      setResultado(r);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  async function ativarSincronismo() {
    setErroAtivar(null);
    try {
      // A varredura só varre pares com profissional E procedimento habilitados — liga os dois.
      if (!profissionalHabilitado)
        await alternarProf.mutateAsync({ id: profissionalId, habilitado: true });
      if (!procedimentoHabilitado)
        await alternarProc.mutateAsync({ id: procedimentoId, habilitado: true });
      setAtivado(true);
    } catch (e) {
      setErroAtivar(extrairMensagemDeErro(e));
    }
  }

  return (
    <Modal
      aberto
      aoFechar={aoFechar}
      titulo="Importar agenda de hoje"
      descricao={`${nomeProcedimento} — ${nomeProfissional}`}
      largura="md"
    >
      {resultado ? (
        <div>
          <div className="flex items-start gap-3 rounded-md border border-emerald-200 bg-emerald-50 p-4">
            <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" />
            <div>
              <p className="font-medium text-emerald-900">{resultado.mensagem}</p>
              <p className="mt-1 text-sm text-emerald-800">
                Hoje ({hojeBr}) · {resultado.requisicoes} requisição(ões) ao SISREG.
              </p>
            </div>
          </div>

          <dl className="mt-4 grid grid-cols-2 gap-3 text-sm sm:grid-cols-4">
            <Indicador rotulo="Encontrados" valor={resultado.totalEncontrados} />
            <Indicador rotulo="Importados" valor={resultado.importados} destaque="verde" />
            <Indicador rotulo="Já existiam" valor={resultado.jaExistiam} />
            <Indicador
              rotulo="Pendências"
              valor={resultado.pendencias}
              destaque={resultado.pendencias > 0 ? 'amarelo' : undefined}
            />
          </dl>

          {resultado.pendencias > 0 && (
            <p className="mt-3 rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
              {resultado.pendencias} agendamento(s) ficaram em pendência (ex.: paciente sem CNS ou
              CADSUS indisponível no momento). Aparecem na aba de erros para tratamento.
            </p>
          )}

          {/* Sugestão de ativar o sincronismo automático — o ponto do recurso: a importação manual
              é paliativa; o certo é o par entrar na varredura diária. */}
          {!jaSincroniza && !ativado && (
            <div className="mt-4 rounded-md border border-blue-200 bg-blue-50 p-4">
              <p className="text-sm text-blue-900">
                Este procedimento <strong>não sincroniza automaticamente</strong> — por isso você
                precisou importar à mão. Quer ativar o sincronismo diário para não precisar repetir?
              </p>
              {erroAtivar && (
                <p className="mt-2 rounded border border-red-200 bg-red-50 p-2 text-sm text-red-700">
                  {erroAtivar}
                </p>
              )}
              <div className="mt-3 flex gap-2">
                <Button tamanho="sm" onClick={ativarSincronismo} disabled={ativando}>
                  {ativando ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <RefreshCw className="h-4 w-4" />
                  )}
                  Sim, ativar sincronismo
                </Button>
                <Button variante="ghost" tamanho="sm" onClick={aoFechar} disabled={ativando}>
                  Agora não
                </Button>
              </div>
            </div>
          )}

          {ativado && (
            <p className="mt-4 flex items-center gap-2 rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
              <CheckCircle2 className="h-4 w-4 shrink-0" />
              Sincronismo ativado — este procedimento passa a entrar na varredura diária da unidade.
            </p>
          )}

          {(jaSincroniza || ativado) && (
            <div className="mt-5 flex justify-end">
              <Button tamanho="sm" onClick={aoFechar}>
                Fechar
              </Button>
            </div>
          )}
        </div>
      ) : (
        <div>
          <p className="text-sm text-gray-600">
            Lê a agenda de <strong>hoje ({hojeBr})</strong> deste profissional × procedimento direto
            no SISREG e cria as solicitações. Recurso pontual — só o dia, para não pesar no limite de
            requisições do SISREG.
          </p>

          {!jaSincroniza && (
            <p className="mt-3 rounded-md border border-blue-200 bg-blue-50 p-3 text-sm text-blue-800">
              Este par ainda não está no sincronismo automático. Depois de importar, dá para ativá-lo
              aqui mesmo.
            </p>
          )}

          {erro && (
            <p className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              {erro}
            </p>
          )}

          <div className="mt-5 flex justify-end gap-2">
            <Button variante="ghost" tamanho="sm" onClick={aoFechar} disabled={importar.isPending}>
              Cancelar
            </Button>
            <Button tamanho="sm" onClick={importarHoje} disabled={importar.isPending}>
              {importar.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Download className="h-4 w-4" />
              )}
              Importar agenda de hoje
            </Button>
          </div>
        </div>
      )}
    </Modal>
  );
}

function Indicador({
  rotulo,
  valor,
  destaque,
}: {
  rotulo: string;
  valor: number;
  destaque?: 'verde' | 'amarelo';
}) {
  const cor =
    destaque === 'verde'
      ? 'text-emerald-700'
      : destaque === 'amarelo'
        ? 'text-amber-700'
        : 'text-gray-900';
  return (
    <div className="rounded-md border border-gray-200 bg-white p-2 text-center">
      <dd className={`text-lg font-semibold ${cor}`}>{valor}</dd>
      <dt className="text-xs text-gray-500">{rotulo}</dt>
    </div>
  );
}
