import { useState } from 'react';
import { Loader2, Paperclip, Save } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Campo } from '@/shared/ui/Campo';
import { Input } from '@/shared/ui/Input';
import { useRegrasAnexo, useSalvarRegrasAnexo, type DestinoComAnexo } from '../../api/regrasAnexoApi';

function emMb(valor: number): string {
  return valor.toLocaleString('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

/**
 * Tamanho máximo por arquivo no envio automático ao destino. Fica na aba de configuração de cada
 * destino (SER, SERNIT) porque cada sistema tem o seu limite — e o "5 MB" que a tela do SER declara
 * nunca foi medido no servidor de lá. Acima do limite, o envio comprime o arquivo para caber.
 */
export function RegrasAnexoEnvio({ destino, nome }: { destino: DestinoComAnexo; nome: string }) {
  const { data } = useRegrasAnexo(destino);
  const salvar = useSalvarRegrasAnexo(destino);
  const [valorEdit, setValorEdit] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  // O que está na caixa: o que a pessoa digitou, ou o que veio do servidor.
  const valor = valorEdit ?? (data ? emMb(data.tamanhoMaximoMb) : '');

  async function aoSalvar() {
    setErro(null);
    setAviso(null);
    const mb = Number(valor.replace(',', '.'));
    if (!valor.trim() || !Number.isFinite(mb)) {
      setErro('Informe o tamanho em MB, por exemplo 4,5.');
      return;
    }
    try {
      const salvo = await salvar.mutateAsync(mb);
      setValorEdit(null);
      setAviso(`Limite do ${nome} salvo: ${emMb(salvo.tamanhoMaximoMb)} MB por arquivo.`);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    }
  }

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4">
      <h3 className="mb-3 flex items-center gap-2 text-sm font-semibold">
        <Paperclip className="size-4 text-red-600" /> Anexos no envio ao {nome}
      </h3>
      <div className="flex flex-wrap items-end gap-3">
        <Campo label="Tamanho máximo por arquivo (MB)" htmlFor={`${destino}-anexo-mb`} className="w-60">
          <Input
            id={`${destino}-anexo-mb`}
            inputMode="decimal"
            value={valor}
            onChange={(e) => setValorEdit(e.target.value)}
          />
        </Campo>
        <Button
          variante="secundaria"
          onClick={() => void aoSalvar()}
          disabled={salvar.isPending || valorEdit === null}
        >
          {salvar.isPending ? <Loader2 className="size-4 animate-spin" /> : <Save className="size-4" />}
          Salvar limite
        </Button>
        {data && !data.configurado && (
          <span className="pb-2 text-xs text-slate-500">Nada configurado ainda: vale o padrão.</span>
        )}
      </div>

      {erro && <p className="mt-3 rounded bg-red-50 p-3 text-sm text-red-700">{erro}</p>}
      {aviso && <p className="mt-3 rounded bg-green-50 p-3 text-sm text-green-800">{aviso}</p>}

      <p className="mt-3 text-xs text-slate-500">
        O {nome} aceita no máximo 2 arquivos por solicitação; com mais, eles vão juntos num PDF. Arquivo
        acima deste limite é <strong>comprimido no envio</strong>: a foto é reduzida e o PDF tem as imagens
        regravadas em resolução menor, nunca abaixo de 150 dpi. Só se nem assim couber o envio é recusado. O
        original continua guardado aqui. A tela do {nome} declara 5 MB, mas nunca se mediu como o servidor de
        lá conta — o padrão de 4,5 MB deixa essa folga. MB como o Windows mostra.
      </p>
    </section>
  );
}
