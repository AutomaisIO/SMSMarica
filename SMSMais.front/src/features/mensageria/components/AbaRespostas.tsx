import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Input } from '@/shared/ui/Input';
import { Paginacao } from '@/shared/ui/Paginacao';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { NomePacienteComResumo } from '@/features/pacientes/components/NomePacienteComResumo';
import { useRespostas } from '@/features/mensageria/api/queries';
import { CLASSE_RESPOSTA, ROTULO_RESPOSTA, dataHora, rotuloCanal, useDebounce } from '@/features/mensageria/lib/rotulos';
import type { RespostaConfirmacao } from '@/features/mensageria/types';

const TAMANHOS = [50, 100, 200] as const;

export function AbaRespostas() {
  const [resposta, setResposta] = useState('Cancelada');
  const [texto, setTexto] = useState('');
  const [de, setDe] = useState('');
  const [ate, setAte] = useState('');
  const [pagina, setPagina] = useState(1);
  const [tamanho, setTamanho] = useState<number>(50);
  const textoDeb = useDebounce(texto);

  useEffect(() => setPagina(1), [resposta, textoDeb, de, ate, tamanho]);

  const q = useRespostas({
    resposta: resposta || undefined,
    texto: textoDeb.trim() || undefined,
    de: de ? `${de}T00:00:00` : undefined,
    ate: ate ? `${ate}T00:00:00` : undefined,
    pagina,
    tamanho,
  });

  const colunas: Coluna<RespostaConfirmacao>[] = useMemo(
    () => [
      {
        chave: 'paciente',
        cabecalho: 'Paciente',
        render: (r) => <NomePacienteComResumo pacienteId={r.pacienteId} nome={r.pacienteNome ?? '(sem nome)'} />,
      },
      {
        chave: 'resposta',
        cabecalho: 'Resposta',
        render: (r) => <span className={`badge ${CLASSE_RESPOSTA[r.statusConfirmacao]}`}>{ROTULO_RESPOSTA[r.statusConfirmacao]}</span>,
      },
      {
        chave: 'motivo',
        cabecalho: 'Motivo informado',
        render: (r) =>
          r.statusConfirmacao === 'Cancelada' ? (
            r.motivo ? <span className="whitespace-pre-wrap">{r.motivo}</span> : <span className="text-gray-400">não informou</span>
          ) : (
            <span className="text-gray-300">—</span>
          ),
      },
      {
        chave: 'proc',
        cabecalho: 'Procedimento',
        render: (r) => {
          const rota = r.exameId ? `/app/solicitacoes-exame/${r.exameId}` : `/app/consultas/${r.solicitacaoId}`;
          return (
            <Link to={rota} className="text-red-700 hover:underline">
              {r.procedimento ?? (r.categoria === 'Consulta' ? 'Consulta' : 'Exame')}
            </Link>
          );
        },
      },
      { chave: 'sisreg', cabecalho: 'Nº SISREG', render: (r) => r.codigoSolicitacao ?? '—' },
      { chave: 'unidade', cabecalho: 'Unidade executante', render: (r) => r.unidadeExecutante ?? '—' },
      { chave: 'data', cabecalho: 'Agendado para', render: (r) => dataHora(r.dataAgendada) },
      { chave: 'quando', cabecalho: 'Respondeu em', render: (r) => dataHora(r.respondidoEm) },
      { chave: 'canal', cabecalho: 'Canal', render: (r) => rotuloCanal(r.canal) },
    ],
    [],
  );

  return (
    <div className="space-y-4">
      <p className="text-sm text-gray-600">
        Resposta dada pelo paciente ao aviso de agendamento (ou registrada pela atendente no menu Confirmações).
        <strong> Fica só no SMSMais</strong> — nada é alterado no SISREG.
      </p>
      <div className="grid grid-cols-1 gap-3 md:grid-cols-5">
        <Select value={resposta} onChange={(e) => setResposta(e.target.value)} aria-label="Resposta">
          <option value="Cancelada">Não vão (cancelaram)</option>
          <option value="Confirmada">Confirmaram</option>
          <option value="">Todas as respostas</option>
        </Select>
        <div className="relative md:col-span-2">
          <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" />
          <Input value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Nome, CPF ou nº SISREG…" className="pl-9" />
        </div>
        <div className="flex items-center gap-2 md:col-span-2">
          <Input type="date" value={de} onChange={(e) => setDe(e.target.value)} aria-label="Respondeu de" />
          <span className="text-sm text-gray-400">até</span>
          <Input type="date" value={ate} onChange={(e) => setAte(e.target.value)} aria-label="Respondeu até" />
        </div>
      </div>
      {q.isError ? <p className="text-sm text-red-700">{extrairMensagemDeErro(q.error)}</p> : null}
      <Tabela colunas={colunas} dados={q.data?.itens ?? []} chaveLinha={(r) => r.solicitacaoId} carregando={q.isLoading} vazio="Nenhuma resposta no filtro." />
      {q.data ? (
        <Paginacao pagina={pagina} tamanho={tamanho} total={q.data.total} tamanhos={TAMANHOS} aoMudarPagina={setPagina} aoMudarTamanho={setTamanho} />
      ) : null}
    </div>
  );
}
