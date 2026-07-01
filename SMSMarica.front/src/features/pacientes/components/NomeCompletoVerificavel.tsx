import { useState } from 'react';
import { BadgeCheck, ShieldAlert, ShieldCheck } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { consultarCpf } from '@/shared/api/integracoes';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { useAtualizarNomePaciente } from '@/features/pacientes/api/queries';
import type { Paciente } from '@/features/pacientes/types';

type Fase =
  | { tipo: 'fechado' }
  | { tipo: 'confere'; nomeRetornado: string }
  | { tipo: 'divergente'; nomeRetornado: string };

/** Normaliza para comparação: sem acento, maiúsculas, espaços colapsados. */
function normalizar(valor: string): string {
  return valor
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toUpperCase();
}

type BotaoProps = {
  pacienteId: string;
  nomeCompleto: string;
  cpf: string;
  dataNascimento: string | null;
  /** Chamado após salvar com sucesso (ex.: atualizar o campo do formulário). */
  onSalvo?: (novoNome: string) => void;
};

/**
 * Botão "Verificar" + modal: recheca o CPF no motor de busca e compara o nome
 * retornado com o do cadastro. Se diferente, oferece corrigir para o nome
 * verificado; se igual, avisa que confere mas ainda permite ajuste manual.
 * Salvar grava via endpoint dedicado (auditado no backend).
 */
export function VerificarNomeBotao({ pacienteId, nomeCompleto, cpf, dataNascimento, onSalvo }: BotaoProps) {
  const [fase, setFase] = useState<Fase>({ tipo: 'fechado' });
  const [consultando, setConsultando] = useState(false);
  const [erroConsulta, setErroConsulta] = useState<string | null>(null);
  const [nomeManual, setNomeManual] = useState('');
  const [erroSalvar, setErroSalvar] = useState<string | null>(null);
  const atualizarNome = useAtualizarNomePaciente();

  const dataNasc = dataNascimento?.slice(0, 10) ?? null;

  async function verificar() {
    if (!dataNasc) return;
    setErroConsulta(null);
    setConsultando(true);
    try {
      const r = await consultarCpf(cpf, dataNasc);
      const retornado = (r.nome ?? '').trim();
      const igual = normalizar(retornado) === normalizar(nomeCompleto);
      setNomeManual(igual ? nomeCompleto : retornado);
      setErroSalvar(null);
      setFase(
        igual
          ? { tipo: 'confere', nomeRetornado: retornado }
          : { tipo: 'divergente', nomeRetornado: retornado },
      );
    } catch (e) {
      setErroConsulta(extrairMensagemDeErro(e));
    } finally {
      setConsultando(false);
    }
  }

  function fechar() {
    setFase({ tipo: 'fechado' });
    setErroSalvar(null);
  }

  async function salvar() {
    const nome = nomeManual.trim();
    if (nome.length < 3) return;
    setErroSalvar(null);
    try {
      await atualizarNome.mutateAsync({ id: pacienteId, nomeCompleto: nome });
      onSalvo?.(nome);
      fechar();
    } catch (e) {
      setErroSalvar(extrairMensagemDeErro(e));
    }
  }

  const nomeAlterado =
    nomeManual.trim().length >= 3 && normalizar(nomeManual) !== normalizar(nomeCompleto);

  return (
    <>
      <button
        type="button"
        onClick={verificar}
        disabled={consultando || !dataNasc}
        title={
          dataNasc
            ? 'Recheca o CPF no motor de busca e compara o nome'
            : 'Cadastre a data de nascimento para verificar'
        }
        className="inline-flex items-center gap-1 rounded-md border border-gray-200 px-2 py-1 text-xs font-medium text-gray-600 transition-colors hover:border-red-300 hover:text-red-700 disabled:cursor-not-allowed disabled:opacity-50"
      >
        <BadgeCheck className="h-3.5 w-3.5" />
        {consultando ? 'Verificando…' : 'Verificar'}
      </button>
      {erroConsulta ? <span className="ml-2 text-xs text-red-600">{erroConsulta}</span> : null}

      <Modal
        aberto={fase.tipo !== 'fechado'}
        aoFechar={fechar}
        titulo="Verificar nome do paciente"
        largura="md"
      >
        {fase.tipo !== 'fechado' ? (
          <div className="space-y-4">
            {fase.tipo === 'divergente' ? (
              <div className="flex items-start gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
                <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0" />
                <span>
                  O nome retornado pelo motor de busca é <strong>diferente</strong> do cadastro.
                  Confira e corrija se necessário.
                </span>
              </div>
            ) : (
              <div className="flex items-start gap-2 rounded-md border border-green-200 bg-green-50 px-3 py-2 text-sm text-green-800">
                <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" />
                <span>
                  O nome <strong>confere</strong> com o registro da Receita Federal. Se ainda assim
                  estiver errado, ajuste manualmente abaixo.
                </span>
              </div>
            )}

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="flex flex-col gap-0.5">
                <span className="text-xs font-medium uppercase tracking-wide text-gray-500">
                  Nome no cadastro
                </span>
                <span className="text-sm text-gray-900">{nomeCompleto}</span>
              </div>
              <div className="flex flex-col gap-0.5">
                <span className="text-xs font-medium uppercase tracking-wide text-gray-500">
                  Nome retornado (Receita)
                </span>
                <span className="text-sm text-gray-900">{fase.nomeRetornado || '—'}</span>
              </div>
            </div>

            {fase.tipo === 'divergente' ? (
              <Button
                variante="outline"
                tamanho="sm"
                onClick={() => setNomeManual(fase.nomeRetornado)}
              >
                Usar nome verificado
              </Button>
            ) : null}

            <div className="flex flex-col gap-1">
              <label
                htmlFor="nome-verificado"
                className="text-xs font-medium uppercase tracking-wide text-gray-500"
              >
                Nome a salvar
              </label>
              <Input
                id="nome-verificado"
                value={nomeManual}
                onChange={(e) => setNomeManual(e.target.value)}
                maxLength={200}
                autoComplete="off"
              />
              <span className="text-xs text-gray-400">
                Altera o nome oficial no hub FHIR e fica registrado na auditoria.
              </span>
            </div>

            {erroSalvar ? (
              <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                {erroSalvar}
              </div>
            ) : null}

            <div className="flex justify-end gap-2 border-t border-gray-100 pt-3">
              <Button variante="ghost" onClick={fechar} disabled={atualizarNome.isPending}>
                Cancelar
              </Button>
              <Button onClick={salvar} disabled={!nomeAlterado || atualizarNome.isPending}>
                {atualizarNome.isPending ? 'Salvando…' : 'Salvar nome'}
              </Button>
            </div>
          </div>
        ) : null}
      </Modal>
    </>
  );
}

/**
 * Campo "Nome completo" (rótulo + valor) com o botão "Verificar" ao lado.
 * Usado na tela de detalhe do paciente.
 */
export function NomeCompletoVerificavel({ paciente }: { paciente: Paciente }) {
  return (
    <div className="flex flex-col gap-0.5">
      <div className="flex items-center justify-between gap-2">
        <span className="text-xs font-medium uppercase tracking-wide text-gray-500">
          Nome completo
        </span>
        <VerificarNomeBotao
          pacienteId={paciente.id}
          nomeCompleto={paciente.nomeCompleto}
          cpf={paciente.cpf}
          dataNascimento={paciente.dataNascimento}
        />
      </div>
      <span className="text-sm text-gray-900">
        {paciente.nomeCompleto || <span className="text-gray-400">—</span>}
      </span>
    </div>
  );
}
