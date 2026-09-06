import { useState } from 'react';
import { AlertTriangle, IdCard, Loader2, Search, UserPlus, X } from 'lucide-react';

import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Modal } from '@/shared/ui/Modal';
import { notificar } from '@/shared/ui/Notificacoes';
import { useDebounce } from '@/shared/hooks/useDebounce';
import { cpfValido } from '@/shared/lib/cpf';

import {
  useBuscarPacienteLocal,
  useConfirmarCadsus,
  useConsultarCadsus,
  useInformarCpf,
} from '../../api/queries';
import type { PacienteCadsus, PacienteResumoRegulacao } from '../../types';

type Props = {
  value: PacienteResumoRegulacao | null;
  onChange: (p: PacienteResumoRegulacao | null) => void;
  /** Vem de `GET regulacao/configuracao/fluxo`. */
  exigirCpf: boolean;
};

/**
 * Passo "paciente" do wizard (plano 10).
 *
 * <p>A ordem é local → CADSUS → criar, e ela existe para não duplicar cidadão: o mesmo paciente
 * cadastrado duas vezes não se desfaz depois, e passa a ter metade do histórico em cada
 * registro. Por isso a consulta ao CADSUS é um botão, nunca automática.</p>
 */
export function PassoPaciente({ value, onChange, exigirCpf }: Props) {
  const [termo, setTermo] = useState('');
  const debounced = useDebounce(termo, 300);
  const busca = useBuscarPacienteLocal(debounced);

  const cadsus = useConsultarCadsus();
  const confirmar = useConfirmarCadsus();
  const [achadoCadsus, setAchadoCadsus] = useState<PacienteCadsus | null>(null);
  const [semCadsus, setSemCadsus] = useState(false);

  const [informandoCpf, setInformandoCpf] = useState(false);

  const digitos = debounced.replace(/\D/g, '');
  const pareceDocumento = digitos.length === 11 || digitos.length === 15;
  const buscou = debounced.trim().length >= 3 && !busca.isLoading;
  const vazio = buscou && (busca.data?.length ?? 0) === 0;

  async function buscarNoCadsus() {
    setSemCadsus(false);
    setAchadoCadsus(null);
    try {
      setAchadoCadsus(await cadsus.mutateAsync(digitos));
    } catch (e) {
      // 404 é resposta: o paciente não existe no CADSUS e o caminho é cadastrar pela tela de
      // Pacientes. Os demais erros já são notificados pelo interceptor.
      const status = (e as { response?: { status?: number } })?.response?.status;
      if (status === 404) setSemCadsus(true);
    }
  }

  async function usarDoCadsus() {
    if (!achadoCadsus) return;
    const p = await confirmar.mutateAsync(achadoCadsus);
    onChange(p);
    setAchadoCadsus(null);
    notificar(p.cpfPendente ? 'Paciente selecionado — falta o CPF.' : 'Paciente selecionado.');
  }

  if (value) {
    return (
      <>
        <div className="rounded-md border border-red-200 bg-red-50/60 p-3">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <p className="truncate font-medium text-slate-900">{value.nome}</p>
              <dl className="mt-1 flex flex-wrap gap-x-4 gap-y-0.5 text-xs text-slate-600">
                <div>
                  <dt className="inline text-slate-400">CPF: </dt>
                  <dd className="inline">{value.cpf ?? '—'}</dd>
                </div>
                <div>
                  <dt className="inline text-slate-400">CNS: </dt>
                  <dd className="inline">{value.cns ?? '—'}</dd>
                </div>
                <div>
                  <dt className="inline text-slate-400">Nascimento: </dt>
                  <dd className="inline">
                    {value.nascimento
                      ? new Date(`${value.nascimento}T00:00:00`).toLocaleDateString('pt-BR')
                      : '—'}
                  </dd>
                </div>
              </dl>
            </div>
            <button
              type="button"
              onClick={() => onChange(null)}
              className="shrink-0 rounded p-1 text-slate-500 hover:bg-red-100"
              aria-label="Trocar paciente"
            >
              <X className="size-4" />
            </button>
          </div>

          {value.cpfPendente ? (
            <div className="mt-2 flex flex-wrap items-center gap-2 border-t border-red-200 pt-2">
              <span className="inline-flex items-center gap-1 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-medium text-amber-800">
                <AlertTriangle className="size-3" /> CPF pendente
              </span>
              <span className="text-xs text-slate-600">
                {exigirCpf
                  ? 'Sem CPF a solicitação salva como rascunho, mas não vai para a fila.'
                  : 'O CPF pode ser informado depois.'}
              </span>
              <Button tamanho="sm" variante="outline" onClick={() => setInformandoCpf(true)}>
                <IdCard className="mr-1 size-3.5" /> Informar CPF
              </Button>
            </div>
          ) : null}
        </div>

        {informandoCpf ? (
          <InformarCpfModal
            paciente={value}
            onFechar={() => setInformandoCpf(false)}
            onResolvido={(p) => {
              onChange(p);
              setInformandoCpf(false);
            }}
          />
        ) : null}
      </>
    );
  }

  return (
    <div className="space-y-3">
      <div className="relative">
        <Search className="pointer-events-none absolute left-3 top-2.5 size-4 text-gray-400" />
        <Input
          value={termo}
          onChange={(e) => {
            setTermo(e.target.value);
            setAchadoCadsus(null);
            setSemCadsus(false);
          }}
          placeholder="CPF, CNS ou nome do paciente"
          className="pl-9"
          autoFocus
        />
      </div>

      {(busca.data?.length ?? 0) > 0 ? (
        <ul className="divide-y divide-slate-100 rounded-md border border-slate-200 bg-white">
          {busca.data!.map((p) => (
            <li key={p.id}>
              <button
                type="button"
                onClick={() => onChange(p)}
                className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left hover:bg-slate-50"
              >
                <span className="min-w-0">
                  <span className="block truncate font-medium text-slate-900">{p.nome}</span>
                  <span className="block text-xs text-slate-500">
                    {p.cpf ?? 'sem CPF'}
                    {p.nascimento
                      ? ` · ${new Date(`${p.nascimento}T00:00:00`).toLocaleDateString('pt-BR')}`
                      : ''}
                  </span>
                </span>
                {p.cpfPendente ? (
                  <span className="shrink-0 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] text-amber-800">
                    CPF pendente
                  </span>
                ) : null}
              </button>
            </li>
          ))}
        </ul>
      ) : null}

      {vazio && pareceDocumento && !achadoCadsus ? (
        <div className="rounded-md border border-slate-200 bg-slate-50 p-3">
          <p className="text-sm text-slate-600">
            Nenhum paciente com esse documento no nosso cadastro.
          </p>
          <Button
            tamanho="sm"
            variante="outline"
            className="mt-2"
            disabled={cadsus.isPending}
            onClick={buscarNoCadsus}
          >
            {cadsus.isPending ? (
              <Loader2 className="mr-1 size-3.5 animate-spin" />
            ) : (
              <Search className="mr-1 size-3.5" />
            )}
            Buscar no CADSUS
          </Button>
        </div>
      ) : null}

      {vazio && !pareceDocumento ? (
        <p className="text-sm text-slate-500">
          Nenhum paciente encontrado. Para buscar no CADSUS, digite o CPF ou o CNS completo.
        </p>
      ) : null}

      {semCadsus ? (
        <p className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          Este documento também não existe no CADSUS. Cadastre o paciente pela tela de Pacientes
          antes de abrir a solicitação.
        </p>
      ) : null}

      {achadoCadsus ? (
        <CartaoPacienteCadsus
          dados={achadoCadsus}
          confirmando={confirmar.isPending}
          onConfirmar={usarDoCadsus}
          onDescartar={() => setAchadoCadsus(null)}
        />
      ) : null}
    </div>
  );
}

/** O que o CADSUS devolveu, para o operador conferir antes de virar cadastro nosso. */
export function CartaoPacienteCadsus({
  dados,
  confirmando,
  onConfirmar,
  onDescartar,
}: {
  dados: PacienteCadsus;
  confirmando: boolean;
  onConfirmar: () => void;
  onDescartar: () => void;
}) {
  return (
    <div className="rounded-md border border-emerald-200 bg-emerald-50/60 p-3">
      <div className="flex items-start gap-2">
        <UserPlus className="mt-0.5 size-4 shrink-0 text-emerald-700" />
        <div className="min-w-0 flex-1">
          <p className="font-medium text-slate-900">{dados.nome}</p>
          <dl className="mt-1 flex flex-wrap gap-x-4 gap-y-0.5 text-xs text-slate-600">
            <div>
              <dt className="inline text-slate-400">CPF: </dt>
              <dd className="inline">{dados.cpf ?? '—'}</dd>
            </div>
            <div>
              <dt className="inline text-slate-400">CNS: </dt>
              <dd className="inline">{dados.cns ?? '—'}</dd>
            </div>
            <div>
              <dt className="inline text-slate-400">Nascimento: </dt>
              <dd className="inline">
                {dados.nascimento
                  ? new Date(`${dados.nascimento}T00:00:00`).toLocaleDateString('pt-BR')
                  : '—'}
              </dd>
            </div>
            {dados.nomeMae ? (
              <div>
                <dt className="inline text-slate-400">Mãe: </dt>
                <dd className="inline">{dados.nomeMae}</dd>
              </div>
            ) : null}
          </dl>
          <p className="mt-1 text-[11px] text-slate-400">fonte: {dados.fonte}</p>
        </div>
      </div>
      <div className="mt-2 flex gap-2">
        <Button tamanho="sm" disabled={confirmando} onClick={onConfirmar}>
          {confirmando ? <Loader2 className="mr-1 size-3.5 animate-spin" /> : null}
          Confirmar e usar
        </Button>
        <Button tamanho="sm" variante="ghost" onClick={onDescartar}>
          Descartar
        </Button>
      </div>
    </div>
  );
}

/**
 * Informa o CPF de um paciente que entrou sem ele.
 *
 * <p>Se o CPF pertencer a outro cadastro, o servidor devolve 409 com o id do outro — a tela
 * oferece trocar para aquele cadastro, em vez de deixar o operador sem saída (o que levaria a
 * criar um terceiro registro).</p>
 */
export function InformarCpfModal({
  paciente,
  onFechar,
  onResolvido,
}: {
  paciente: PacienteResumoRegulacao;
  onFechar: () => void;
  onResolvido: (p: PacienteResumoRegulacao) => void;
}) {
  const informar = useInformarCpf();
  const [cpf, setCpf] = useState('');
  const [erro, setErro] = useState<string | null>(null);

  const digitos = cpf.replace(/\D/g, '');
  const podeSalvar = digitos.length === 11 && cpfValido(digitos);

  async function salvar() {
    setErro(null);
    try {
      onResolvido(await informar.mutateAsync({ id: paciente.id, cpf: digitos }));
    } catch (e) {
      const resp = (e as { response?: { status?: number; data?: { detail?: string } } })?.response;
      if (resp?.status === 409) setErro(resp.data?.detail ?? 'Este CPF já pertence a outro cadastro.');
    }
  }

  return (
    <Modal aberto titulo={`Informar CPF — ${paciente.nome}`} aoFechar={onFechar}>
      <div className="space-y-3">
        <Input
          value={cpf}
          onChange={(e) => setCpf(e.target.value)}
          placeholder="000.000.000-00"
          autoFocus
        />
        {digitos.length === 11 && !cpfValido(digitos) ? (
          <p className="text-xs font-medium text-red-600">CPF inválido — confira os dígitos.</p>
        ) : null}
        {erro ? (
          <p className="rounded border border-amber-200 bg-amber-50 p-2 text-xs text-amber-800">
            {erro}
          </p>
        ) : null}
        <div className="flex justify-end gap-2">
          <Button variante="ghost" onClick={onFechar}>
            Cancelar
          </Button>
          <Button disabled={!podeSalvar || informar.isPending} onClick={salvar}>
            {informar.isPending ? <Loader2 className="mr-1 size-4 animate-spin" /> : null}
            Salvar
          </Button>
        </div>
      </div>
    </Modal>
  );
}
