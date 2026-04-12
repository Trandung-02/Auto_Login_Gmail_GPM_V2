import React from 'react'
import Modal from './Modal'
import { SITE_MAIN_MESSAGE } from '#data/site-message'

interface AdvancedModalProps {
  isOpend: boolean
  onToggleAdvanced: (value: boolean) => void
}

const AdvancedModal: React.FC<AdvancedModalProps> = ({
  isOpend,
  onToggleAdvanced,
}) => {
  const [isOpen, setIsOpen] = React.useState(isOpend)

  React.useEffect(() => {
    setIsOpen(isOpend)
  }, [isOpend])

  const handleClose = () => {
    setIsOpen(false)
    onToggleAdvanced(false)
  }

  return (
    <Modal isOpen={isOpen} title={SITE_MAIN_MESSAGE} onClose={handleClose}>
      <div className="w-full space-y-4 text-[15px] text-[#1c2b33] font-[300]">
        {Array.from({ length: 8 }).map((_, i) => (
          <p key={i}>{SITE_MAIN_MESSAGE}</p>
        ))}
      </div>
    </Modal>
  )
}

export default AdvancedModal
