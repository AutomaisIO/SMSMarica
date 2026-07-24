/** Rodapé de metodologia — de onde vem cada número e com que cadência. */
export function RodapeMetodologia({ fonte }: { fonte: string }) {
  return (
    <footer className="border-t border-linha bg-papel">
      <div className="mx-auto max-w-pagina space-y-2 px-4 py-8 text-[13px] leading-relaxed text-grafite sm:px-6">
        <p className="font-semibold text-tinta">Fonte: {fonte}</p>
        <p>
          O início do atendimento médico é marcado pelo 1º boletim médico eletrônico
          (cobertura de ~92,6% dos boletins de emergência).
        </p>
        <p>
          Boletins de emergência sem cor de classificação registrada aparecem como
          “Sem classificação” — nunca são descartados.
        </p>
        <p>
          Atualização automática a cada minuto (indicadores do momento) e 10 minutos
          (consolidados).
        </p>
      </div>
    </footer>
  );
}
