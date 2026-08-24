import { useState } from 'react';
import { ClipboardList, Loader2, Search } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { Tabela, type Coluna } from '@/shared/ui/Tabela';
import { consultarSisreg } from '@/features/sisreg/api/sisregApi';
import { CONSULTAS_SISREG, type ConsultaSisreg, type RegistroSisreg } from '@/features/sisreg/types';

function hoje(): string {
  return new Date().toISOString().slice(0, 10);
}

function fmtData(v?: string | null): string {
  if (!v) return '—';
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleDateString('pt-BR');
}

export function SisregConsultaPage() {
  const [consulta, setConsulta] = useState<ConsultaSisreg>('fila');
  const [inicio, setInicio] = useState(hoje());
  const [fim, setFim] = useState(hoje());
  const [tamanho, setTamanho] = useState(100);

  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<{ total: number; itens: RegistroSisreg[] } | null>(null);

  const meta = CONSULTAS_SISREG.find((c) => c.id === consulta)!;

  type Linha = RegistroSisreg & { _idx: number };
  const linhas: Linha[] = (resultado?.itens ?? []).map((r, i) => ({ ...r, _idx: i }));

  async function aoConsultar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setCarregando(true);
    setResultado(null);
    try {
      const r = await consultarSisreg(consulta, {
        inicio: meta.usaIntervalo ? inicio : undefined,
        fim: meta.usaIntervalo ? fim : undefined,
        tamanho,
      });
      setResultado(r);
    } catch (err) {
      setErro(extrairMensagemDeErro(err));
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<Linha>[] = [
    { chave: 'codigo', cabecalho: 'Solicitação', render: (r) => r.codigoSolicitacao ?? '—' },
    {
      chave: 'paciente',
      cabecalho: 'Paciente',
      render: (r) => (
        <div className="min-w-0">
          <div className="truncate font-medium text-gray-900">{r.nomeUsuario ?? '—'}</div>
          <div className="truncate text-xs text-gray-500">CNS {r.cnsUsuario ?? '—'}</div>
        </div>
      ),
    },
    {
      chave: 'procedimento',
      cabecalho: 'Procedimento',
      render: (r) => r.descricaoInternaProcedimento ?? r.descricaoProcedimento ?? '—',
    },
    {
      chave: 'status',
      cabecalho: 'Status',
      render: (r) => r.statusSolicitacao ?? r.status ?? r.siglaSituacao ?? '—',
    },
    {
      chave: 'data',
      cabecalho: 'Data',
      render: (r) =>
        fmtData(r.dataMarcacao ?? r.dataConfirmacao ?? r.dataAprovacao ?? r.dataInternacao ?? r.dataSolicitacao),
    },
    { chave: 'unidade', cabecalho: 'Unidade', render: (r) => r.nomeUnidadeExecutante ?? r.nomeUnidadeSolicitante ?? '—' },
  ];

  return (
    <div className="space-y-6">
      <header>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <ClipboardList className="h-6 w-6 text-primary-600" />
          Consultar SISREG
        </h1>
        <p className="mt-1 text-sm text-gray-600">
          Feed de leitura do SISREG (DATASUS). As credenciais e centrais são definidas em Configuração SISREG.
        </p>
      </header>

      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <form onSubmit={aoConsultar} className="grid grid-cols-1 items-end gap-4 sm:grid-cols-2 lg:grid-cols-5">
          <Campo label="Consulta" htmlFor="sr-consulta" className="lg:col-span-2">
            <Select id="sr-consulta" value={consulta} onChange={(e) => setConsulta(e.target.value as ConsultaSisreg)}>
              {CONSULTAS_SISREG.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.rotulo}
                </option>
              ))}
            </Select>
          </Campo>

          {meta.usaIntervalo ? (
            <>
              <Campo label="Início" htmlFor="sr-inicio">
                <Input id="sr-inicio" type="date" value={inicio} onChange={(e) => setInicio(e.target.value)} />
              </Campo>
              <Campo label="Fim" htmlFor="sr-fim">
                <Input id="sr-fim" type="date" value={fim} onChange={(e) => setFim(e.target.value)} />
              </Campo>
            </>
          ) : (
            <div className="hidden lg:col-span-2 lg:block" />
          )}

          <Campo label="Máx. registros" htmlFor="sr-tam">
            <Input
              id="sr-tam"
              type="number"
              min={1}
              max={1000}
              value={tamanho}
              onChange={(e) => setTamanho(Number(e.target.value) || 100)}
            />
          </Campo>

          <div className="sm:col-span-2 lg:col-span-5 flex justify-end">
            <Button type="submit" disabled={carregando}>
              {carregando ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <Search className="mr-2 h-4 w-4" />}
              Consultar
            </Button>
          </div>
        </form>
      </section>

      {erro ? (
        <div className="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">{erro}</div>
      ) : null}

      {resultado ? (
        <section className="space-y-3">
          <p className="text-sm text-gray-600">
            {resultado.total} registro(s) encontrados · exibindo {resultado.itens.length}.
          </p>
          <Tabela
            colunas={colunas}
            dados={linhas}
            chaveLinha={(r) => String(r._idx)}
            carregando={false}
            vazio="Nenhum registro para os filtros informados."
          />
        </section>
      ) : null}
    </div>
  );
}
