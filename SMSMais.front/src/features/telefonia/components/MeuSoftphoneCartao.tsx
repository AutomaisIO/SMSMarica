import { useState } from 'react';
import { Headset, Mic } from 'lucide-react';
import { useMeuSoftphone } from '@/features/telefonia/api/queries';
import { useSoftphone } from '@/features/telefonia/store/softphoneStore';
import { Button } from '@/shared/ui/Button';

const ROTULO = {
  desligado: 'desligado',
  outraAba: 'funcionando em outra aba',
  conectando: 'conectando',
  registrado: 'pronto para ligar',
  falhou: 'sem registro',
} as const;

/**
 * Softphone no Meu Perfil: só leitura. Quem habilita e escolhe o ramal é o admin, na edição
 * do usuário. Aqui o usuário vê o ramal, o estado e testa o microfone antes da primeira ligação.
 */
export function MeuSoftphoneCartao() {
  const meu = useMeuSoftphone();
  const registro = useSoftphone((s) => s.registro);
  const [teste, setTeste] = useState<'ok' | 'negado' | null>(null);

  if (!meu.data?.habilitado) return null;

  async function testarMicrofone() {
    setTeste(null);
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      stream.getTracks().forEach((t) => t.stop());
      setTeste('ok');
    } catch {
      setTeste('negado');
    }
  }

  return (
    <section className="rounded-lg border border-gray-200 bg-white p-4">
      <h2 className="mb-2 flex items-center gap-2 text-sm font-semibold text-gray-900">
        <Headset className="h-4 w-4" /> Softphone
      </h2>
      <p className="text-sm text-gray-700">
        Ramal <strong>{meu.data.ramal}</strong> — {meu.data.nomeExibicao}
        {meu.data.ativo ? <> · {ROTULO[registro]}</> : <> · desativado pelo administrador</>}
      </p>
      <p className="mt-1 text-xs text-gray-500">
        O telefone fica no botão redondo no canto da tela. Para mudar o ramal ou o nome, fale com o administrador.
      </p>
      <div className="mt-3 flex items-center gap-3">
        <Button type="button" variante="outline" onClick={testarMicrofone}>
          <Mic className="h-4 w-4" /> Testar microfone
        </Button>
        {teste === 'ok' ? <span className="text-sm text-emerald-700">Microfone liberado.</span> : null}
        {teste === 'negado' ? (
          <span className="text-sm text-red-700">
            O navegador não liberou o microfone. Clique no cadeado da barra de endereço e permita.
          </span>
        ) : null}
      </div>
    </section>
  );
}
