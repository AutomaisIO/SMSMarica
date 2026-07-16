import type { ReactNode } from 'react';
import { ArrowLeft, Pencil } from 'lucide-react';
import { TelefoneCopiavel } from '@/shared/ui/TelefoneCopiavel';
import { useNavigate, useParams } from 'react-router-dom';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Avatar } from '@/shared/ui/Avatar';
import { Button } from '@/shared/ui/Button';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { useMedicoPorId } from '@/features/medicos/api/queries';
import type { EnderecoDto } from '@/features/medicos/types';

function formatarCpf(cpf: string) {
  const d = cpf.replace(/\D/g, '');
  return d.length === 11 ? `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}` : cpf;
}

function formatarData(iso: string | null): string | null {
  if (!iso) return null;
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

function formatarEndereco(e: EnderecoDto | null): string | null {
  if (!e) return null;
  const linha1 = [e.logradouro, e.numero].filter(Boolean).join(', ');
  const partes = [linha1, e.complemento, e.bairro, [e.cidade, e.uf].filter(Boolean).join('/'), e.cep]
    .map((p) => p?.trim())
    .filter(Boolean);
  return partes.length > 0 ? partes.join(' · ') : null;
}

function Dado({ rotulo, valor }: { rotulo: string; valor?: ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs font-medium uppercase tracking-wide text-gray-500">{rotulo}</span>
      <span className="text-sm text-gray-900">{valor || <span className="text-gray-400">—</span>}</span>
    </div>
  );
}

export function MedicoDetalhePage() {
  const navigate = useNavigate();
  const params = useParams<{ id: string }>();
  const id = params.id ?? '';

  const detalhe = useMedicoPorId(id || null);
  const m = detalhe.data;
  const ehMedico = !m || m.conselho === 'CRM' || !m.conselho;
  const voltarPara = ehMedico ? '/app/medicos' : '/app/profissionais';

  return (
    <div className="space-y-6">
      <header className="flex items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <button
            type="button"
            onClick={() => navigate(voltarPara)}
            className="rounded-md p-2 text-gray-600 hover:bg-gray-100"
            aria-label="Voltar"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <Avatar src={m?.fotoBase64} nome={m?.nomeCompleto} tamanho="lg" />
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-semibold text-gray-900">
                {m?.nomeCompleto ?? 'Carregando…'}
              </h1>
              {m ? <StatusBadge ativo={m.usuarioAtivo} /> : null}
            </div>
            {m ? (
              <p className="text-sm text-gray-500">
                {m.conselho}-{m.ufConselho} {m.registro}
                {m.especialidade ? ` · ${m.especialidade}` : ''}
              </p>
            ) : null}
          </div>
        </div>
        <Button variante="outline" onClick={() => navigate('/app/medicos')}>
          <Pencil className="h-4 w-4" /> Editar na lista
        </Button>
      </header>

      {detalhe.isLoading ? (
        <div className="text-sm text-gray-500">Carregando médico…</div>
      ) : detalhe.isError ? (
        <div className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          {extrairMensagemDeErro(detalhe.error)}
        </div>
      ) : m ? (
        <div className="rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
          <div className="grid grid-cols-1 gap-5 md:grid-cols-2">
            <Dado rotulo="CPF" valor={formatarCpf(m.cpf)} />
            <Dado rotulo="Data de nascimento" valor={formatarData(m.dataNascimento) ?? undefined} />
            <Dado rotulo="Conselho" valor={`${m.conselho}-${m.ufConselho}`} />
            <Dado rotulo="Registro" valor={m.registro} />
            <Dado rotulo="Especialidade" valor={m.especialidade ?? undefined} />
            <Dado rotulo="RQE" valor={m.rqe ?? undefined} />
            <Dado rotulo="Validade do registro" valor={formatarData(m.validadeRegistro) ?? undefined} />
            <Dado rotulo="Telefone" valor={m.telefone ? <TelefoneCopiavel numero={m.telefone} /> : undefined} />
            <Dado rotulo="Cadastrado em" valor={new Date(m.criadoEm).toLocaleString('pt-BR')} />
            <div className="md:col-span-2">
              <Dado rotulo="Endereço" valor={formatarEndereco(m.endereco)} />
            </div>
          </div>
        </div>
      ) : null}
    </div>
  );
}
