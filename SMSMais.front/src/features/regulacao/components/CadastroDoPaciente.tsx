import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import { AlertTriangle, BellOff, CheckCircle2, Circle, ShieldCheck } from 'lucide-react';

import { usePacientePorId } from '@/features/pacientes/api/queries';
import { BotoesEnriquecerFicha } from '@/features/pacientes/components/EnriquecerFicha';
import type { Paciente } from '@/features/pacientes/types';
import type { PacienteResumoRegulacao } from '../types';
import { BotaoVerificarTelefonePaciente } from '@/features/telefone-validacao/components/BotaoVerificarTelefonePaciente';
import { usePermissao } from '@/shared/auth/authStore';
import { formatarTelefoneBR } from '@/shared/ui/TelefoneCopiavel';

function digitos(v: string | null | undefined): string {
  let d = (v ?? '').replace(/\D/g, '');
  if ((d.length === 12 || d.length === 13) && d.startsWith('55')) d = d.slice(2);
  return d;
}

/** Celular com DDD e 9 dígitos — a regra do envio ao SERNIT (`PacienteNaTela.Celular`). */
function celular(p: Paciente): string | null {
  const negado = digitos(p.telefoneNegado);
  return (
    [p.telefoneVerificado, p.telefoneCelular, p.telefonePrincipal]
      .map(digitos)
      .find((d) => d.length === 11 && d[2] === '9' && d !== negado) ?? null
  );
}

/**
 * O que o SERNIT exige no painel do paciente para gravar o pedido (medido em 09/10/2026, PR-23).
 * O SERNIT não consulta o CADSUS: paciente que ele não conhece é cadastrado lá com estes dados da
 * nossa ficha — e, sem eles, o pedido volta recusado.
 */
const EXIGIDOS_SERNIT: { rotulo: string; tem: (p: Paciente) => boolean }[] = [
  { rotulo: 'Nome', tem: (p) => !!p.nomeCompleto?.trim() },
  { rotulo: 'CPF', tem: (p) => digitos(p.cpf).length === 11 },
  { rotulo: 'Sexo', tem: (p) => p.sexo === 'Masculino' || p.sexo === 'Feminino' },
  { rotulo: 'Data de nascimento', tem: (p) => !!p.dataNascimento },
  { rotulo: 'Nome da mãe', tem: (p) => !!p.nomeDaMae?.trim() },
  { rotulo: 'Logradouro', tem: (p) => !!p.endereco?.logradouro?.trim() },
  { rotulo: 'UF', tem: (p) => !!p.endereco?.uf?.trim() },
  { rotulo: 'Município', tem: (p) => !!p.endereco?.cidade?.trim() },
  { rotulo: 'Celular', tem: (p) => celular(p) !== null },
];

/**
 * O resumo do paciente que a solicitação guarda, atualizado com a ficha — ou null se nada mudou.
 * A ficha muda por aqui quando o CPF ou o nascimento vêm do CADSUS/e-SUS.
 */
export function resumoDaFicha(p: Paciente, atual: PacienteResumoRegulacao): PacienteResumoRegulacao | null {
  const cpf = digitos(p.cpf);
  const nascimento = p.dataNascimento?.slice(0, 10) ?? null;
  if (cpf === digitos(atual.cpf) && nascimento === (atual.nascimento ?? null) && p.nomeCompleto === atual.nome) {
    return null;
  }
  return {
    ...atual,
    nome: p.nomeCompleto,
    cpf: cpf.length === 11 ? cpf : atual.cpf,
    nascimento,
    cpfPendente: cpf.length !== 11,
  };
}

type Props = {
  pacienteId: string;
  /** Sistema de destino da solicitação ('Sernit', 'Ser', 'EsusSg', 'Sisreg') ou nulo (interno). */
  destino: string | null;
  /** Chamado com a ficha sempre que ela carrega ou muda (enriquecer, telefone confirmado). */
  aoAtualizar?: (p: Paciente) => void;
};

/**
 * O cadastro do paciente visto por quem está pedindo: se ele chega ao destino completo e se o
 * paciente vai receber os avisos.
 *
 * <p><b>Telefone confirmado</b> é o que garante o aviso: é por ele que a plataforma manda o
 * agendamento pelo WhatsApp, e é ele que vai no "Telefone Celular" do SERNIT. Número que ninguém
 * confirmou pode ser de outra pessoa.</p>
 *
 * <p><b>SERNIT:</b> mostra o que ele exige e o que falta na ficha, com os botões que buscam no
 * CADSUS e no e-SUS — para completar antes de mandar, e não descobrir na hora do envio.</p>
 */
export function CadastroDoPaciente({ pacienteId, destino, aoAtualizar }: Props) {
  const paciente = usePacientePorId(pacienteId);
  const podeEditar = usePermissao('Pacientes', 'Edicao');
  const p = paciente.data;

  useEffect(() => {
    if (p) aoAtualizar?.(p);
    // Só quando a ficha muda — o callback costuma ser uma função nova a cada render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [p]);

  if (!p) {
    return paciente.isLoading ? <p className="text-xs text-slate-400">Carregando o cadastro…</p> : null;
  }

  const sernit = destino === 'Sernit';
  const faltando = sernit ? EXIGIDOS_SERNIT.filter((e) => !e.tem(p)) : [];
  const confirmado = digitos(p.telefoneVerificado);
  const sugerido = celular(p) ?? p.telefonePrincipal ?? null;

  return (
    <div className="space-y-3 rounded-md border border-slate-200 bg-white p-3">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-slate-500">Cadastro do paciente</h3>
        {podeEditar ? (
          <div className="flex flex-wrap items-center gap-2">
            <span className="text-xs text-slate-500">Buscar e completar:</span>
            <BotoesEnriquecerFicha pacienteId={pacienteId} aoGravar={() => void paciente.refetch()} />
            <Link to={`/app/pacientes/${pacienteId}/editar`} className="text-xs text-primary-700 hover:underline">
              Editar a ficha
            </Link>
          </div>
        ) : null}
      </div>

      {/* Telefone: é o que decide se o paciente fica sabendo do agendamento. */}
      {confirmado ? (
        <p className="flex items-start gap-2 text-sm text-emerald-800">
          <ShieldCheck className="mt-0.5 size-4 shrink-0" />
          <span>
            Telefone confirmado: <b>{formatarTelefoneBR(confirmado)}</b>. É por ele que o paciente recebe os avisos
            desta solicitação.
          </span>
        </p>
      ) : (
        <div className="rounded-md border border-amber-300 bg-amber-50 p-2 text-sm text-amber-900">
          <p className="flex items-start gap-2">
            <BellOff className="mt-0.5 size-4 shrink-0" />
            <span>
              <b>Telefone não confirmado.</b> Sem um número confirmado, o paciente pode não receber os avisos
              desta solicitação — data marcada, mudança, cancelamento. Confirme o número com ele: chega um código
              pelo WhatsApp e ele diz o código.
            </span>
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-2 pl-6">
            {digitos(p.cpf).length === 11 ? (
              <BotaoVerificarTelefonePaciente
                cpf={p.cpf}
                numeroInicial={sugerido}
                aoValidado={() => void paciente.refetch()}
              />
            ) : (
              <span className="text-xs">Para confirmar o telefone, o paciente precisa ter CPF no cadastro.</span>
            )}
            {p.telefoneNegado ? (
              <span className="text-xs">
                O número {formatarTelefoneBR(p.telefoneNegado)} foi negado (“não sou essa pessoa”) e não é usado.
              </span>
            ) : null}
          </div>
        </div>
      )}

      {sernit ? (
        <div>
          <p className="mb-1 text-xs text-slate-500">
            O SERNIT só aceita o pedido com estes dados. Quando ele não conhece o paciente, a plataforma o cadastra
            lá com a nossa ficha.
          </p>
          <ul className="flex flex-wrap gap-1.5">
            {EXIGIDOS_SERNIT.map((e) => {
              const tem = e.tem(p);
              return (
                <li
                  key={e.rotulo}
                  className={
                    tem
                      ? 'inline-flex items-center gap-1 rounded bg-emerald-50 px-1.5 py-0.5 text-[11px] text-emerald-800'
                      : 'inline-flex items-center gap-1 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-medium text-amber-900'
                  }
                >
                  {tem ? <CheckCircle2 className="size-3" /> : <Circle className="size-3" />}
                  {e.rotulo}
                </li>
              );
            })}
          </ul>
          {faltando.length > 0 ? (
            <p className="mt-2 flex items-start gap-2 text-sm text-amber-900">
              <AlertTriangle className="mt-0.5 size-4 shrink-0" />
              <span>
                Falta na ficha: <b>{faltando.map((f) => f.rotulo.toLowerCase()).join(', ')}</b>. Busque no CADSUS ou
                no e-SUS, ou complete na ficha — sem isso o SERNIT recusa o pedido no envio.
              </span>
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
