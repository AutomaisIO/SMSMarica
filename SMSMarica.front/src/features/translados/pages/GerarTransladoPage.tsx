import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  ArrowLeft,
  Bus,
  CheckCircle2,
  Loader2,
  MapPin,
  Sparkles,
  TriangleAlert,
  Wand2,
} from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useGerarTranslado } from '@/features/translados/api/queries';
import type { ResultadoGeracao } from '@/features/translados/types';

function dataLocalHoje(): string {
  const d = new Date();
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const dd = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${mm}-${dd}`;
}

function formatarDataBr(iso: string): string {
  const m = iso.match(/^(\d{4})-(\d{2})-(\d{2})/);
  return m ? `${m[3]}/${m[2]}/${m[1]}` : iso;
}

function formatarKm(metros: number): string {
  return `${(metros / 1000).toFixed(1).replace('.', ',')} km`;
}

function formatarDuracao(segundos: number): string {
  const min = Math.round(segundos / 60);
  if (min < 60) return `${min} min`;
  const h = Math.floor(min / 60);
  return `${h}h ${min % 60}min`;
}

function Selo({ cor, children }: { cor: string; children: React.ReactNode }) {
  return (
    <span className={`inline-flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs ${cor}`}>
      {children}
    </span>
  );
}

export function GerarTransladoPage() {
  const navigate = useNavigate();
  const podeGerar = usePermissao('Translados', 'Inclusao');
  const gerar = useGerarTranslado();

  const [data, setData] = useState(dataLocalHoje());
  const [usarIa, setUsarIa] = useState(true);
  const [resultado, setResultado] = useState<ResultadoGeracao | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  function executar(confirmar: boolean) {
    if (confirmar && !window.confirm(
      `Gerar e salvar as rotas de ${formatarDataBr(data)}? As rotas geradas anteriormente para esta data (não concluídas) serão recriadas.`,
    )) {
      return;
    }
    setErro(null);
    gerar.mutate(
      { data, confirmar, usarIa },
      {
        onSuccess: (r) => setResultado(r),
        onError: (e) => setErro(extrairMensagemDeErro(e)),
      },
    );
  }

  return (
    <div className="space-y-6">
      <header className="space-y-1">
        <Link to="/app/translados" className="inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-800">
          <ArrowLeft className="h-4 w-4" /> Translados
        </Link>
        <h1 className="flex items-center gap-2 text-2xl font-semibold text-gray-900">
          <Wand2 className="h-6 w-6 text-primary-600" />
          Gerar translado automático
        </h1>
        <p className="text-sm text-gray-600">
          Distribui os pacientes elegíveis do dia nos veículos e otimiza a ordem de coleta. A
          pré-visualização não grava nada — confira as rotas e os não alocados antes de salvar.
        </p>
      </header>

      <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Campo label="Data" htmlFor="gt-data">
            <Input id="gt-data" type="date" value={data} onChange={(e) => setData(e.target.value)} />
          </Campo>
          <div className="flex items-end">
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input type="checkbox" checked={usarIa} onChange={(e) => setUsarIa(e.target.checked)} />
              Usar IA (Claude) para distribuir nos veículos
            </label>
          </div>
        </div>

        <div className="mt-5 flex flex-wrap items-center justify-end gap-3">
          <Button type="button" variante="outline" onClick={() => executar(false)} disabled={gerar.isPending}>
            {gerar.isPending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
            Pré-visualizar
          </Button>
          <Button
            type="button"
            onClick={() => executar(true)}
            disabled={gerar.isPending || !podeGerar}
            title={podeGerar ? undefined : 'Você não tem permissão para gerar translados.'}
          >
            Gerar e salvar
          </Button>
        </div>

        {erro ? (
          <div className="mt-4 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</div>
        ) : null}
      </section>

      {resultado ? <Resultado resultado={resultado} aoAbrirLista={() => navigate('/app/translados')} /> : null}
    </div>
  );
}

function Resultado({ resultado, aoAbrirLista }: { resultado: ResultadoGeracao; aoAbrirLista: () => void }) {
  return (
    <div className="space-y-4">
      <section className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
        <div className="flex flex-wrap items-center gap-2">
          {resultado.confirmado ? (
            <Selo cor="bg-emerald-50 text-emerald-800 border-emerald-200">
              <CheckCircle2 className="h-3.5 w-3.5" /> Gravado
            </Selo>
          ) : (
            <Selo cor="bg-blue-50 text-blue-800 border-blue-200">Pré-visualização (não gravado)</Selo>
          )}
          <Selo cor="bg-violet-50 text-violet-800 border-violet-200">
            <Sparkles className="h-3.5 w-3.5" /> {resultado.usouIa ? 'Distribuição: IA (Claude)' : 'Distribuição: heurística'}
          </Selo>
          <Selo cor="bg-gray-50 text-gray-700 border-gray-200">
            {resultado.aproximado ? 'Distâncias aproximadas' : 'Distâncias Google Routes'}
          </Selo>
          <span className="ml-auto text-sm text-gray-600">
            {resultado.totalAlocadas} de {resultado.totalSessoes} sessões alocadas ·{' '}
            {resultado.rotas.length} rota(s)
          </span>
        </div>

        {resultado.confirmado ? (
          <div className="mt-3">
            <Button variante="outline" tamanho="sm" onClick={aoAbrirLista}>
              Ver translados
            </Button>
          </div>
        ) : null}
      </section>

      <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
        {resultado.rotas.map((r, i) => (
          <div key={r.rotaId ?? `${r.veiculoId}-${i}`} className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
            <div className="flex items-start justify-between gap-2">
              <div className="flex items-center gap-2">
                <Bus className="h-5 w-5 text-primary-600" />
                <div>
                  <div className="font-medium text-gray-900">{r.veiculoPlaca} · {r.motoristaNome}</div>
                  <div className="text-xs text-gray-500">Destino: {r.unidadeNome}</div>
                </div>
              </div>
              <div className="text-right text-xs text-gray-500">
                <div>{r.qtdPacientes} paciente(s)</div>
                <div>{formatarKm(r.distanciaTotalMetros)} · {formatarDuracao(r.duracaoEstimadaSegundos)}</div>
              </div>
            </div>

            <ol className="mt-3 space-y-1 border-t border-gray-100 pt-3">
              {r.paradas.map((p) => (
                <li key={p.sessaoId} className="flex items-center gap-2 text-sm text-gray-700">
                  <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-primary-50 text-[11px] font-medium text-primary-700">
                    {p.ordem}
                  </span>
                  <span>{p.pacienteNome}</span>
                  {p.comAcompanhante ? (
                    <span className="rounded-full bg-amber-50 px-1.5 text-[10px] font-medium text-amber-700">+ acompanhante</span>
                  ) : null}
                </li>
              ))}
            </ol>
          </div>
        ))}
      </div>

      {resultado.naoAlocadas.length > 0 ? (
        <section className="rounded-xl border border-amber-200 bg-amber-50/50 p-4">
          <h3 className="flex items-center gap-2 text-sm font-semibold text-amber-800">
            <TriangleAlert className="h-4 w-4" />
            {resultado.naoAlocadas.length} não alocado(s)
          </h3>
          <ul className="mt-2 space-y-1">
            {resultado.naoAlocadas.map((s) => (
              <li key={s.sessaoId} className="flex flex-wrap items-center gap-x-2 text-sm text-gray-700">
                <MapPin className="h-3.5 w-3.5 text-amber-600" />
                <span className="font-medium">{s.pacienteNome}</span>
                <span className="text-gray-500">({s.unidadeNome})</span>
                <span className="text-amber-700">— {s.motivo}</span>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </div>
  );
}
