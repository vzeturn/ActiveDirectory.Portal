import { useContext, useId, useState } from 'react'
import * as Popover from '@radix-ui/react-popover'
import { ModalBoundaryContext } from '../Modal/ModalBoundaryContext'
import { useComboboxSearch } from '../../../hooks/useComboboxSearch'
import styles from './Combobox.module.css'

export interface ComboboxOption {
  value: string
  label: string
}

interface ComboboxProps {
  value: string | null
  onChange: (value: string | null) => void
  onSearch: (query: string) => Promise<ComboboxOption[]>
  placeholder?: string
  disabled?: boolean
  title?: string
}

export function Combobox({ value, onChange, onSearch, placeholder, disabled, title }: ComboboxProps) {
  const [query, setQuery] = useState(value ?? '')
  const [isOpen, setIsOpen] = useState(false)
  const listboxId = useId()
  const boundaryEl = useContext(ModalBoundaryContext)

  const { options, isSearching, highlightedIndex, handleKeyDown } = useComboboxSearch<ComboboxOption>({
    isOpen,
    query,
    onSearch
  })

  const selectOption = (opt: ComboboxOption) => {
    onChange(opt.value)
    setQuery(opt.label)
    setIsOpen(false)
  }

  const isPopoverOpen = isOpen && (options.length > 0 || isSearching)

  return (
    <div className={styles.wrapper}>
      <Popover.Root open={isPopoverOpen}>
        <Popover.Anchor asChild>
          <input
            type="text"
            role="combobox"
            aria-expanded={isPopoverOpen}
            aria-controls={listboxId}
            aria-activedescendant={
              highlightedIndex >= 0 ? `${listboxId}-option-${highlightedIndex}` : undefined
            }
            className={styles.input}
            value={query}
            placeholder={placeholder}
            disabled={disabled}
            title={title}
            onChange={(e) => {
              setQuery(e.target.value)
              setIsOpen(true)
              if (e.target.value === '') onChange(null)
            }}
            onFocus={() => setIsOpen(true)}
            onBlur={() => {
              setTimeout(() => setIsOpen(false), 150)
            }}
            onKeyDown={(e) => handleKeyDown(e, selectOption, () => setIsOpen(false))}
          />
        </Popover.Anchor>
        <Popover.Portal>
          <Popover.Content
            className={styles.optionList}
            data-in-modal={boundaryEl ? 'true' : undefined}
            asChild
            side="bottom"
            align="start"
            sideOffset={4}
            avoidCollisions
            collisionBoundary={boundaryEl ?? undefined}
            onOpenAutoFocus={(e) => e.preventDefault()}
            onCloseAutoFocus={(e) => e.preventDefault()}
            onInteractOutside={(e) => e.preventDefault()}
          >
            <ul id={listboxId} role="listbox">
              {isSearching && options.length === 0 && (
                <li className={styles.searchingRow} role="presentation">
                  Searching…
                </li>
              )}
              {options.map((opt, index) => (
                <li key={opt.value} role="presentation">
                  <button
                    id={`${listboxId}-option-${index}`}
                    type="button"
                    role="option"
                    aria-selected={index === highlightedIndex}
                    className={
                      index === highlightedIndex
                        ? `${styles.option} ${styles.optionHighlighted}`
                        : styles.option
                    }
                    onClick={() => selectOption(opt)}
                  >
                    {opt.label}
                  </button>
                </li>
              ))}
            </ul>
          </Popover.Content>
        </Popover.Portal>
      </Popover.Root>
    </div>
  )
}
