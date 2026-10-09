import { useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { UserPlus } from 'lucide-react';

import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';

import {
  buscarMedicosParecidos,
  idPendente,
  obterMedicoPendente,
  resolverMedicoPendente,
  type MedicoParecido,
} from '../api/medicosApi';
import { ROTULO_SISTEMA_REGULACAO } from '../types';

/**
 * O médico que a unidade pediu na abertura e ainda não está no sistema de destino.
 *
 * <p>É o técnico da regulação quem cadastra no SER/SERNIT — pela tela de lá, no ícone "Adicionar
 * médico" ao lado do combo do médico — e confirma aqui. Enquanto o médico estiver pendente, o
 * "Registrar envio" fica barrado: o sistema só aceita médico da lista dele.</p>
 *
 * <p>Três desfechos: <b>cadastrei</b>; <b>já existia</b> com outro nome (o SER abrevia muito — a
 * solicitação passa a usar o nome de lá); ou <b>recusado</b>, com o motivo que a unidade lê.</p>
 */
export function MedicoPendenteCard({
  valorMedico,
  podeResolver,
}: {
  /** O valor do campo de médico da solicitação (`pendente:{id}` quando é pedido). */
  valorMedico: unknown;
  podeResolver: boolean;
}) {
  const id = idPendente(typeof valorMedico === 'string' ? valorMedico : null);
  const qc = useQueryClient();
  const pendente = useQuery({
    queryKey: ['regulacao', 'medicos', 'pendente', id],
    queryFn: () => obterMedicoPendente(id!),
    enabled: !!id,
  });
  const [modo, setModo] = useState<'nada' | 'jaExistia' | 'recusar'>('nada');
  const [parecidos, setParecidos] = useState<MedicoParecido[] | null>(null);
  const [nomeExistente, setNomeExistente] = useState('');
  const [motivo, setMotivo] = useState('');
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  if (!id || !pendente.data) return null;
  const m = pendente.data;
  const sistema = ROTULO_SISTEMA_REGULACAO[m.sistema];

  async function resolver(
    acao: 'Cadastrado' | 'JaExistia' | 'Recusado' | 'Pendente',
    nome: string | null,
    mot: string | null,
  ) {
    setErro(null);
    setOcupado(true);
    try {
      await resolverMedicoPendente(m.id, acao, nome, mot);
      // Resolver troca o médico em todas as solicitações que o usavam: recarrega tudo.
      await qc.invalidateQueries({ queryKey: ['regulacao'] });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  async function abrirJaExistia() {
    setModo('jaExistia');
    try {
      setParecidos(
        (await buscarMedicosParecidos(m.sistema, m.nome, m.numeroDocumento)).filter((p) => p.origem === 'Sistema'),
      );
    } catch {
      setParecidos([]);
    }
  }

  const incerto = m.situacao === 'CadastroIncerto';

  if (m.situacao !== 'Pendente' && !incerto) {
    return (
      <section className="rounded-lg border border-slate-200 bg-white p-4 text-sm">
        <p className="text-slate-700">
          Médico pedido pela unidade: <strong>{m.nome}</strong> —{' '}
          {m.situacao === 'Recusado'
            ? `recusado pela regulação: ${m.motivo ?? ''}. A unidade precisa escolher outro médico.`
            : `resolvido como ${m.nomeNoSistema ?? m.nome} no ${sistema}.`}
        </p>
      </section>
    );
  }

  return (
    <section
      className={`rounded-lg border p-4 ${incerto ? 'border-red-300 bg-red-50' : 'border-amber-300 bg-amber-50'}`}
    >
      <h2 className={`flex items-center gap-2 text-sm font-semibold ${incerto ? 'text-red-900' : 'text-amber-900'}`}>
        <UserPlus className="size-4" />{' '}
        {incerto ? `Cadastro no ${sistema} a conferir` : `Médico novo a cadastrar no ${sistema}`}
      </h2>
      <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 text-sm text-slate-800">
        <dt className="text-slate-500">Nome</dt>
        <dd className="font-medium">{m.nome}</dd>
        <dt className="text-slate-500">Documento</dt>
        <dd>{m.numeroDocumento ? `${m.tipoDocumento} ${m.numeroDocumento}` : '— não informado'}</dd>
        <dt className="text-slate-500">Especialidade</dt>
        <dd>{m.especialidade ?? '— não informada'}</dd>
      </dl>
      {incerto ? (
        <p className="mt-2 text-xs text-red-900">
          A plataforma tentou cadastrar este médico no {sistema} (com a autorização da regulação) e não
          conseguiu confirmar que ele entrou na lista. <strong>Confira no {sistema}</strong>: se o médico está lá,
          “Já existia” e escolha o cadastro; se não está, “Não entrou” — e ele volta a aguardar cadastro.
          Não tente de novo sem conferir: o cadastro do Estado não tem apagar.
        </p>
      ) : (
        <p className="mt-2 text-xs text-amber-900">
          Pelo <strong>Enviar ao {sistema}</strong>, a plataforma mostra os nomes parecidos da lista e, com a sua
          autorização, cadastra o médico lá. Ou, no {sistema}: na tela da solicitação, ícone{' '}
          <strong>Adicionar médico</strong> ao lado de “Médico responsável”, e confirme aqui. O envio só libera
          depois de resolver.
        </p>
      )}

      {podeResolver && modo === 'nada' && (
        <div className="mt-3 flex flex-wrap gap-2">
          {incerto ? (
            <Button onClick={() => resolver('Pendente', null, null)} disabled={ocupado}>
              Não entrou
            </Button>
          ) : (
            <Button onClick={() => resolver('Cadastrado', null, null)} disabled={ocupado}>
              Cadastrei no {sistema}
            </Button>
          )}
          <Button variante="secundaria" onClick={abrirJaExistia} disabled={ocupado}>
            Já existia no {sistema}
          </Button>
          <Button variante="outline" onClick={() => setModo('recusar')} disabled={ocupado}>
            Recusar
          </Button>
        </div>
      )}

      {modo === 'jaExistia' && (
        <div className="mt-3 space-y-2 text-sm">
          <p className="text-slate-700">Qual cadastro do {sistema} é este médico?</p>
          {(parecidos ?? []).map((p) => (
            <button
              key={p.valor}
              type="button"
              onClick={() => setNomeExistente(p.nome)}
              className={`block w-full rounded border px-2 py-1 text-left ${
                nomeExistente === p.nome ? 'border-primary-600 bg-white' : 'border-slate-200 bg-white/60'
              }`}
            >
              {p.nome} <span className="text-xs text-slate-500">· {p.motivo}</span>
            </button>
          ))}
          <Input
            placeholder={`Ou digite o nome exatamente como está no ${sistema}`}
            value={nomeExistente}
            onChange={(e) => setNomeExistente(e.target.value)}
          />
          <div className="flex gap-2">
            <Button onClick={() => resolver('JaExistia', nomeExistente, null)} disabled={ocupado || !nomeExistente.trim()}>
              Usar este cadastro
            </Button>
            <Button variante="ghost" onClick={() => setModo('nada')}>
              Voltar
            </Button>
          </div>
        </div>
      )}

      {modo === 'recusar' && (
        <div className="mt-3 space-y-2 text-sm">
          <Input
            placeholder="Por que não cadastrar? (a unidade vai ler)"
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
          />
          <div className="flex gap-2">
            <Button variante="danger" onClick={() => resolver('Recusado', null, motivo)} disabled={ocupado || !motivo.trim()}>
              Recusar o médico
            </Button>
            <Button variante="ghost" onClick={() => setModo('nada')}>
              Voltar
            </Button>
          </div>
        </div>
      )}

      {erro && <p className="mt-2 text-sm text-red-700">{erro}</p>}
    </section>
  );
}
