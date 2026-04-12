import { configureStore } from "@reduxjs/toolkit"
import stepFormReducer from "./slices/stepFormSlice"

export const store = configureStore({
    reducer: {
        stepForm: stepFormReducer
    }
})

export type RootState = ReturnType<typeof store.getState>
export type AppDispatch = typeof store.dispatch