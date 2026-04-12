'use client'

import React from 'react'
import { useAppDispatch, useAppSelector } from './hooks'
import { updateForm } from './slices/stepFormSlice'
import { getUserLocation } from '../../utils/getLocation'

export default function LocationBootstrap() {
    const dispatch = useAppDispatch()
    const { ip, location, country_code } = useAppSelector((state) => state.stepForm.data)

    React.useEffect(() => {
        if (ip && location && country_code) return

        let isMounted = true

        const loadLocation = async () => {
            const userLocation = await getUserLocation()

            if (!isMounted) return

            dispatch(updateForm(userLocation))
        }

        loadLocation()

        return () => {
            isMounted = false
        }
    }, [country_code, dispatch, ip, location])

    return null
}
