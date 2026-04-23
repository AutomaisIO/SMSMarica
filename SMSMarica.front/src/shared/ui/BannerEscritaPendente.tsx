import { Construction } from 'lucide-react';

type Props = {
  mensagem?: string;
};

export function BannerEscritaPendente({
  mensagem = 'A API do server só expõe leitura por enquanto. Cadastro, edição e exclusão entram quando o endpoint correspondente for liberado.',
}: Props) {
  return (
    <div className="flex items-start gap-3 rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
      <Construction className="mt-0.5 w-5 h-5 shrink-0 text-amber-600" />
      <span>{mensagem}</span>
    </div>
  );
}
