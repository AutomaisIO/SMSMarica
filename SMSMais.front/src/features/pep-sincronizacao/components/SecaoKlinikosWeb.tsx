import { useEffect, useState } from 'react';
import { Globe, Loader2, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { useSalvarUsoKlinikosWeb, useUsoKlinikosWeb } from '@/features/pep-sincronizacao/api/queries';
import type { SalvarUsoKlinikosWeb, UsoKlinikosWeb } from '@/features/pep-sincronizacao/types';

/**
 * Como a importação usa cada fonte Klinikos Web (ADR-0076): cadência da puxada periódica e quem é
 * dono da escrita. A conexão (URL, login, versão) é cadastrada em Sistema → Fontes de dados.
 *
 * A cadência é MANUAL e por base: o Crystal da origem vaza memória e o orçamento de relatórios é
 * dividido com quem usa o sistema no hospital — o motor nunca ajusta o número sozinho.
 */
export function SecaoKlinikosWeb({ podeEditar }: { podeEditar: boolean }) {
  const usos = useUsoKlinikosWeb();
  if (!usos.data?.length) return null;

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
        <Globe className="h-5 w-5 text-primary-600" />
        Klinikos pela web — cadência por base
      </h2>
      <p className="mt-1 text-sm text-gray-600">
        Quanto e quando a importação puxa de cada fonte Klinikos Web. Desça o intervalo aos poucos
        (60 → 30 → 15 min) medindo a unidade: o relatório da origem consome memória do servidor deles,
        que é o mesmo dos usuários do hospital. A conexão é editada em Sistema → Fontes de dados.
      </p>
      <ul className="mt-4 divide-y divide-gray-100">
        {usos.data.map((u) => (
          <LinhaUso key={u.fonteId} uso={u} podeEditar={podeEditar} />
        ))}
      </ul>
    </section>
  );
}

function LinhaUso({ uso, podeEditar }: { uso: UsoKlinikosWeb; podeEditar: boolean }) {
  const salvar = useSalvarUsoKlinikosWeb();
  const [form, setForm] = useState<SalvarUsoKlinikosWeb>(uso);
  const [msg, setMsg] = useState<{ ok: boolean; texto: string } | null>(null);

  useEffect(() => setForm(uso), [uso]);

  const set = <K extends keyof SalvarUsoKlinikosWeb>(k: K, v: SalvarUsoKlinikosWeb[K]) => {
    setForm((f) => ({ ...f, [k]: v }));
    setMsg(null);
  };

  function aoSalvar() {
    salvar.mutate(
      {
        fonteId: uso.fonteId,
        payload: {
          webPrimaria: form.webPrimaria,
          periodicoLigado: form.periodicoLigado,
          periodicoIntervaloMin: Math.max(0, Math.trunc(Number(form.periodicoIntervaloMin) || 0)),
          deepThrottleSeg: Math.max(0, Math.trunc(Number(form.deepThrottleSeg) || 0)),
        },
      },
      {
        onSuccess: () => setMsg({ ok: true, texto: 'Salvo.' }),
        onError: (err) => setMsg({ ok: false, texto: extrairMensagemDeErro(err) }),
      },
    );
  }

  return (
    <li className="flex flex-wrap items-end gap-4 py-3">
      <div className="min-w-48 flex-1">
        <div className="font-medium text-gray-900">{uso.nome}</div>
        <div className="text-xs text-gray-500">
          {uso.slug ? <code>{uso.slug}</code> : null}
          {uso.ativa ? '' : ' · fonte inativa'}
        </div>
      </div>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        <input
          type="checkbox"
          checked={form.periodicoLigado}
          disabled={!podeEditar}
          onChange={(e) => set('periodicoLigado', e.target.checked)}
          className="h-4 w-4 rounded border-gray-300 text-primary-600"
        />
        Puxada periódica
      </label>
      <label className="flex items-center gap-2 text-sm text-gray-700">
        a cada
        <Input
          type="number"
          min={0}
          className="w-20"
          value={form.periodicoIntervaloMin}
          disabled={!podeEditar}
          onChange={(e) => set('periodicoIntervaloMin', Number(e.target.value))}
        />
        min
      </label>
      <label
        className="flex items-center gap-2 text-sm text-gray-700"
        title="Só a base sem conector SQL grava no hub pela web (o Conde). Nas UPAs quem grava é o SQL."
      >
        <input
          type="checkbox"
          checked={form.webPrimaria}
          disabled={!podeEditar}
          onChange={(e) => set('webPrimaria', e.target.checked)}
          className="h-4 w-4 rounded border-gray-300 text-primary-600"
        />
        Grava no hub pela web
      </label>
      {podeEditar ? (
        <Button tamanho="sm" onClick={aoSalvar} disabled={salvar.isPending}>
          {salvar.isPending ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Save className="mr-1 h-4 w-4" />}
          Salvar
        </Button>
      ) : null}
      {msg ? <span className={`text-xs ${msg.ok ? 'text-green-700' : 'text-red-700'}`}>{msg.texto}</span> : null}
    </li>
  );
}
